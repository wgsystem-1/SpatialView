using System.Drawing;
using System.Numerics;
using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 지도 좌표 변환 클래스
/// 월드 좌표와 화면 좌표 간의 변환을 담당
/// </summary>
public class MapTransform
{
    private Matrix3x2 _worldToScreen;
    private Matrix3x2 _screenToWorld;
    private Envelope _viewExtent;
    private Size _canvasSize;
    private bool _isValid;
    
    public MapTransform()
    {
        _worldToScreen = Matrix3x2.Identity;
        _screenToWorld = Matrix3x2.Identity;
        _viewExtent = new Envelope(0, 1, 0, 1);
        _canvasSize = new Size(1, 1);
        _isValid = false;
    }
    
    /// <summary>
    /// 변환 매트릭스 업데이트
    /// </summary>
    public void UpdateTransform(Envelope viewExtent, Size canvasSize)
    {
        if (viewExtent == null || viewExtent.IsNull || 
            canvasSize.Width <= 0 || canvasSize.Height <= 0)
        {
            _isValid = false;
            return;
        }
        
        _viewExtent = viewExtent;
        _canvasSize = canvasSize;
        
        // 스케일 계산
        var scaleX = canvasSize.Width / viewExtent.Width;
        var scaleY = canvasSize.Height / viewExtent.Height;
        
        // World to Screen 변환 매트릭스
        // 1. 원점을 viewExtent의 왼쪽 아래로 이동
        // 2. Y축 반전 (화면 좌표는 위에서 아래로)
        // 3. 스케일 적용
        _worldToScreen = Matrix3x2.CreateTranslation((float)-viewExtent.MinX, (float)-viewExtent.MinY) *
                        Matrix3x2.CreateScale((float)scaleX, (float)-scaleY) *
                        Matrix3x2.CreateTranslation(0, canvasSize.Height);
        
        // 역행렬 계산
        if (Matrix3x2.Invert(_worldToScreen, out var inverted))
        {
            _screenToWorld = inverted;
            _isValid = true;
        }
        else
        {
            _isValid = false;
        }
    }
    
    /// <summary>
    /// 월드 좌표를 화면 좌표로 변환
    /// </summary>
    public PointF WorldToScreen(ICoordinate worldCoordinate)
    {
        if (!_isValid || worldCoordinate == null)
            return new PointF(0, 0);
            
        var worldPoint = new Vector2((float)worldCoordinate.X, (float)worldCoordinate.Y);
        var screenPoint = Vector2.Transform(worldPoint, _worldToScreen);
        
        return new PointF(screenPoint.X, screenPoint.Y);
    }
    
    /// <summary>
    /// 화면 좌표를 월드 좌표로 변환
    /// </summary>
    public ICoordinate ScreenToWorld(PointF screenPoint)
    {
        if (!_isValid)
            return new Coordinate(0, 0);
            
        var screen = new Vector2(screenPoint.X, screenPoint.Y);
        var worldPoint = Vector2.Transform(screen, _screenToWorld);
        
        return new Coordinate(worldPoint.X, worldPoint.Y);
    }
    
    /// <summary>
    /// 월드 좌표를 화면 좌표로 변환 (정수형)
    /// </summary>
    public System.Drawing.Point WorldToScreen(double worldX, double worldY)
    {
        var point = WorldToScreen(new Coordinate(worldX, worldY));
        return new System.Drawing.Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
    }

    /// <summary>
    /// 월드 좌표를 화면 좌표로 변환 (float 정밀도 유지)
    /// </summary>
    public PointF WorldToScreenF(double worldX, double worldY)
    {
        return WorldToScreen(new Coordinate(worldX, worldY));
    }
    
    /// <summary>
    /// 화면 좌표를 월드 좌표로 변환 (정수형)
    /// </summary>
    public ICoordinate ScreenToWorld(int screenX, int screenY)
    {
        return ScreenToWorld(new PointF(screenX, screenY));
    }

    /// <summary>
    /// 화면 좌표를 월드 좌표로 변환 (double 정밀도)
    /// </summary>
    public ICoordinate ScreenToWorld(double screenX, double screenY)
    {
        return ScreenToWorld(new PointF((float)screenX, (float)screenY));
    }
    
    /// <summary>
    /// 월드 거리를 화면 거리로 변환
    /// </summary>
    public double WorldToScreenDistance(double worldDistance)
    {
        if (!_isValid || _viewExtent.Width == 0)
            return 0;
            
        return worldDistance * (_canvasSize.Width / _viewExtent.Width);
    }
    
    /// <summary>
    /// 화면 거리를 월드 거리로 변환
    /// </summary>
    public double ScreenToWorldDistance(double screenDistance)
    {
        if (!_isValid || _canvasSize.Width == 0)
            return 0;
            
        return screenDistance * (_viewExtent.Width / _canvasSize.Width);
    }
    
    /// <summary>
    /// 현재 픽셀당 월드 단위
    /// </summary>
    public double PixelSize
    {
        get
        {
            if (!_isValid || _canvasSize.Width == 0)
                return 1;
                
            return _viewExtent.Width / _canvasSize.Width;
        }
    }
    
    /// <summary>
    /// 변환이 유효한지 여부
    /// </summary>
    public bool IsValid => _isValid;
    
    /// <summary>
    /// 현재 뷰 영역
    /// </summary>
    public Envelope ViewExtent => _viewExtent;
    
    /// <summary>
    /// 캔버스 크기
    /// </summary>
    public Size CanvasSize => _canvasSize;
}