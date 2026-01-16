using System.Drawing;
using SpatialView.Engine.Data.Layers;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Rendering;
using SpatialView.Engine.CoordinateSystems;

namespace SpatialView.Engine;

/// <summary>
/// SpatialView 자체 GIS 엔진의 Map 클래스
/// 레이어 관리, 뷰포트 제어, 렌더링 조정을 담당
/// </summary>
public class MapContainer : Data.IMap, IDisposable
{
    private readonly LayerCollection _layers;
    private readonly LayerCollection _backgroundLayers;
    private string _name = "Map";
    private ICoordinate _center;
    private double _zoom;
    private Size _size;
    private Color _backgroundColor;
    private int _srid;
    private double _minimumZoom;
    private double _maximumZoom;
    private readonly MapTransform _transform;
    private readonly object _syncRoot = new();
    
    public MapContainer()
    {
        _layers = new LayerCollection();
        _backgroundLayers = new LayerCollection();
        _center = new Coordinate(0, 0);
        _zoom = 1000;
        _size = new Size(800, 600);
        _backgroundColor = Color.White;
        _srid = 0;
        _minimumZoom = 0.01;
        _maximumZoom = 1e10;
        _transform = new MapTransform();
        
        // 이벤트 연결
        _layers.LayerAdded += (s, e) => LayersChanged?.Invoke(this, EventArgs.Empty);
        _layers.LayerRemoved += (s, e) => LayersChanged?.Invoke(this, EventArgs.Empty);
        
        // 초기 변환 업데이트
        UpdateTransform();
    }
    
    #region Properties
    
    /// <summary>
    /// 맵 이름
    /// </summary>
    public string Name
    {
        get => _name;
        set => _name = value ?? "Map";
    }
    
    /// <summary>
    /// 지도 레이어 컬렉션 (IMap 인터페이스 구현)
    /// </summary>
    public IList<ILayer> Layers => _layers;
    
    /// <summary>
    /// 지도 레이어 컬렉션 (어댑터에서 사용)
    /// </summary>
    public ILayerCollection LayerCollection => _layers;
    
    /// <summary>
    /// 배경 레이어 컬렉션
    /// </summary>
    public ILayerCollection BackgroundLayers => _backgroundLayers;
    
    /// <summary>
    /// 지도 중심점
    /// </summary>
    public ICoordinate Center
    {
        get => _center;
        set
        {
            if (_center != value)
            {
                _center = value ?? new Coordinate(0, 0);
                OnViewChanged();
            }
        }
    }
    
    /// <summary>
    /// 줌 레벨 (지도 너비를 월드 단위로 나타냄)
    /// </summary>
    public double Zoom
    {
        get => _zoom;
        set
        {
            var newZoom = Math.Max(_minimumZoom, Math.Min(value, _maximumZoom));
            if (Math.Abs(_zoom - newZoom) > double.Epsilon)
            {
                _zoom = newZoom;
                OnViewChanged();
            }
        }
    }
    
    /// <summary>
    /// 지도 캔버스 크기 (픽셀)
    /// </summary>
    public Size Size
    {
        get => _size;
        set
        {
            if (_size != value)
            {
                _size = value;
                UpdateTransform();
                OnViewChanged();
            }
        }
    }
    
    /// <summary>
    /// 배경색
    /// </summary>
    public Color BackgroundColor
    {
        get => _backgroundColor;
        set => _backgroundColor = value;
    }
    
    /// <summary>
    /// 공간 참조 시스템 ID
    /// </summary>
    public int SRID
    {
        get => _srid;
        set => _srid = value;
    }
    
    /// <summary>
    /// 최소 줌 레벨
    /// </summary>
    public double MinimumZoom
    {
        get => _minimumZoom;
        set => _minimumZoom = Math.Max(0.0001, value);
    }
    
    /// <summary>
    /// 최대 줌 레벨
    /// </summary>
    public double MaximumZoom
    {
        get => _maximumZoom;
        set => _maximumZoom = Math.Max(_minimumZoom, value);
    }
    
    /// <summary>
    /// 현재 보이는 영역
    /// </summary>
    public Envelope ViewExtent
    {
        get
        {
            if (_size.Width == 0 || _size.Height == 0)
                return new Envelope(0, 0, 0, 0);
                
            var halfWidth = _zoom * 0.5;
            var halfHeight = (_zoom / _size.Width) * _size.Height * 0.5;
            
            return new Envelope(
                _center.X - halfWidth,
                _center.X + halfWidth,
                _center.Y - halfHeight,
                _center.Y + halfHeight);
        }
        set
        {
            if (value != null && !value.IsNull)
            {
                ZoomToExtent(value);
            }
        }
    }
    
    /// <summary>
    /// 픽셀당 월드 단위
    /// </summary>
    public double PixelSize => _transform.IsValid ? _transform.PixelSize : 1;
    
    #endregion
    
    #region Methods
    
    /// <summary>
    /// 지정된 영역으로 확대/축소
    /// </summary>
    public void ZoomToExtent(Envelope envelope)
    {
        if (envelope == null || envelope.IsNull)
            return;
            
        lock (_syncRoot)
        {
            // 중심점 설정
            _center = new Coordinate(envelope.CenterX, envelope.CenterY);
            
            // 줌 레벨 계산 (여백 10% 추가)
            var zoomX = envelope.Width * 1.1;
            var zoomY = envelope.Height * 1.1 * (_size.Width / (double)_size.Height);
            
            Zoom = Math.Max(zoomX, zoomY);
        }
    }
    
    /// <summary>
    /// 모든 레이어가 보이도록 확대/축소
    /// </summary>
    public void ZoomToExtents()
    {
        var totalEnvelope = GetExtents();
        System.Diagnostics.Debug.WriteLine($"[Map.ZoomToExtents] totalEnvelope={totalEnvelope}");
        if (totalEnvelope != null && !totalEnvelope.IsNull)
        {
            ZoomToExtent(totalEnvelope);
        }
    }
    
    /// <summary>
    /// 전체 레이어의 범위 가져오기 (IMap 인터페이스)
    /// </summary>
    public Envelope GetExtent()
    {
        return GetExtents() ?? new Envelope(0, 0, 0, 0);
    }
    
    /// <summary>
    /// 전체 레이어의 범위 가져오기 (nullable)
    /// </summary>
    public Envelope? GetExtents()
    {
        Envelope? totalEnvelope = null;
        
        System.Diagnostics.Debug.WriteLine($"[Map.GetExtents] 시작 - 레이어 수: {_layers.Count}");
        
        // 일반 레이어의 범위 계산 (Enabled/Visible 무시 - 모든 레이어의 Extent 포함)
        foreach (var layer in _layers)
        {
            var layerEnvelope = GetLayerEnvelope(layer);
            System.Diagnostics.Debug.WriteLine($"[Map.GetExtents] 레이어: {layer.Name}, Extent={layerEnvelope}");
            
            if (layerEnvelope != null && !layerEnvelope.IsNull)
            {
                if (totalEnvelope == null)
                    totalEnvelope = new Envelope(layerEnvelope);
                else
                    totalEnvelope.ExpandToInclude(layerEnvelope);
            }
        }
        
        // 일반 레이어가 없는 경우에만 배경 레이어의 범위 고려
        if (totalEnvelope == null)
        {
            foreach (var layer in _backgroundLayers)
            {
                var layerEnvelope = GetLayerEnvelope(layer);
                if (layerEnvelope != null && !layerEnvelope.IsNull)
                {
                    if (totalEnvelope == null)
                        totalEnvelope = new Envelope(layerEnvelope);
                    else
                        totalEnvelope.ExpandToInclude(layerEnvelope);
                }
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"[Map.GetExtents] 최종 범위: {totalEnvelope}");
        
        return totalEnvelope;
    }
    
    /// <summary>
    /// 월드 좌표를 화면 좌표로 변환 (정수형)
    /// </summary>
    public System.Drawing.Point WorldToScreen(ICoordinate coordinate)
    {
        if (!_transform.IsValid || coordinate == null)
            return new System.Drawing.Point(0, 0);

        return _transform.WorldToScreen(coordinate.X, coordinate.Y);
    }

    /// <summary>
    /// 월드 좌표를 화면 좌표로 변환 (float 정밀도 유지)
    /// </summary>
    public System.Drawing.PointF WorldToScreenF(ICoordinate coordinate)
    {
        if (!_transform.IsValid || coordinate == null)
            return new System.Drawing.PointF(0, 0);

        return _transform.WorldToScreenF(coordinate.X, coordinate.Y);
    }
    
    /// <summary>
    /// 화면 좌표를 월드 좌표로 변환
    /// </summary>
    public ICoordinate ScreenToWorld(System.Drawing.Point point)
    {
        if (!_transform.IsValid)
            return new Coordinate(0, 0);

        return _transform.ScreenToWorld(point.X, point.Y);
    }

    /// <summary>
    /// 화면 좌표를 월드 좌표로 변환 (double 정밀도 유지)
    /// </summary>
    public ICoordinate ScreenToWorld(double screenX, double screenY)
    {
        if (!_transform.IsValid)
            return new Coordinate(0, 0);

        return _transform.ScreenToWorld(screenX, screenY);
    }
    
    /// <summary>
    /// 지도를 지정된 방향으로 이동
    /// </summary>
    public void Pan(double dx, double dy)
    {
        Center = new Coordinate(_center.X + dx, _center.Y + dy);
    }
    
    /// <summary>
    /// 지도를 렌더링 컨텍스트에 그리기
    /// </summary>
    public void Render(IRenderContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
            
        OnMapRendering();
        
        try
        {
            // 배경색 그리기
            context.Clear(_backgroundColor);
            
            // 배경 레이어 렌더링
            RenderLayers(_backgroundLayers, context);
            
            // 일반 레이어 렌더링
            RenderLayers(_layers, context);
        }
        finally
        {
            OnMapRendered();
        }
    }
    
    private void RenderLayers(ILayerCollection layers, IRenderContext context)
    {
        foreach (var layer in layers)
        {
            if (!layer.Enabled) continue;
            
            // 가시성 범위 확인
            if (layer is VectorLayer vectorLayer)
            {
                var scale = GetMapScale();
                if (scale < vectorLayer.MinVisible || scale > vectorLayer.MaxVisible)
                    continue;
            }
            
            OnLayerRendering(layer);
            
            try
            {
                // TODO: 실제 레이어 렌더링 구현
                // layer.Render(context, this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"레이어 '{layer.Name}' 렌더링 오류: {ex.Message}");
            }
            finally
            {
                OnLayerRendered(layer);
            }
        }
    }
    
    /// <summary>
    /// 현재 지도 축척 계산
    /// </summary>
    public double GetMapScale()
    {
        // 일반적인 모니터 DPI (96 dpi) 기준
        const double dpi = 96;
        const double inchesPerMeter = 39.3701;
        
        var metersPerPixel = PixelSize;
        return metersPerPixel * dpi * inchesPerMeter;
    }
    
    /// <summary>
    /// 변환 객체 가져오기 (내부 사용)
    /// </summary>
    internal MapTransform Transform => _transform;
    
    /// <summary>
    /// 레이어 추가
    /// </summary>
    public void AddLayer(ILayer layer)
    {
        if (layer != null)
        {
            _layers.Add(layer);
        }
    }
    
    /// <summary>
    /// 레이어 제거
    /// </summary>
    public bool RemoveLayer(ILayer layer)
    {
        if (layer != null)
        {
            return _layers.Remove(layer);
        }
        return false;
    }
    
    /// <summary>
    /// 이름으로 레이어 찾기
    /// </summary>
    public ILayer? GetLayerByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
            
        return _layers.FirstOrDefault(l => l.Name == name) ??
               _backgroundLayers.FirstOrDefault(l => l.Name == name);
    }
    
    /// <summary>
    /// 맵 새로고침
    /// </summary>
    public void Refresh()
    {
        OnViewChanged();
    }
    
    private Envelope? GetLayerEnvelope(ILayer layer)
    {
        // 레이어의 Extent 속성 반환
        return layer.Extent;
    }
    
    #endregion
    
    #region Events
    
    public event EventHandler? ViewChanged;
    public event EventHandler? LayersChanged;
    public event EventHandler? MapRendering;
    public event EventHandler? MapRendered;
    public event EventHandler<LayerEventArgs>? LayerRendering;
    public event EventHandler<LayerEventArgs>? LayerRendered;
    
    protected virtual void OnViewChanged()
    {
        UpdateTransform();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// 좌표 변환 업데이트
    /// </summary>
    private void UpdateTransform()
    {
        _transform.UpdateTransform(ViewExtent, _size);
    }
    
    protected virtual void OnMapRendering()
    {
        MapRendering?.Invoke(this, EventArgs.Empty);
    }
    
    protected virtual void OnMapRendered()
    {
        MapRendered?.Invoke(this, EventArgs.Empty);
    }
    
    protected virtual void OnLayerRendering(ILayer layer)
    {
        LayerRendering?.Invoke(this, new LayerEventArgs(layer));
    }
    
    protected virtual void OnLayerRendered(ILayer layer)
    {
        LayerRendered?.Invoke(this, new LayerEventArgs(layer));
    }
    
    #endregion
    
    #region IDisposable
    
    private bool _disposed;
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        
        if (disposing)
        {
            _layers.Clear();
            _backgroundLayers.Clear();
        }
        
        _disposed = true;
    }
    
    #endregion
    
    #region Testing Methods
    
    /// <summary>
    /// 기본 기능 테스트 (개발/디버깅용)
    /// </summary>
    public static bool TestBasicFunctionality()
    {
        try
        {
            Console.WriteLine("🧪 SpatialView 엔진 기본 기능 테스트 시작...");
            
            // MapContainer 생성 테스트
            var map = new MapContainer();
            map.Size = new Size(800, 600);
            map.Center = new Coordinate(126.978, 37.5665); // 서울시청
            map.Zoom = 12.0;
            map.SRID = 4326;
            
            if (map.Size.Width != 800 || map.Size.Height != 600)
            {
                Console.WriteLine("❌ 지도 크기 설정 실패");
                return false;
            }
            
            if (Math.Abs(map.Center.X - 126.978) > 0.001 || Math.Abs(map.Center.Y - 37.5665) > 0.001)
            {
                Console.WriteLine("❌ 지도 중심점 설정 실패");
                return false;
            }
            
            // 좌표 변환 테스트
            var screenCenter = new System.Drawing.Point(400, 300);
            var worldCoord = map.ScreenToWorld(screenCenter);
            var backToScreen = map.WorldToScreen(worldCoord);
            
            if (Math.Abs(backToScreen.X - 400) > 10 || Math.Abs(backToScreen.Y - 300) > 10)
            {
                Console.WriteLine("❌ 좌표 변환 실패");
                return false;
            }
            
            // 레이어 생성 테스트
            var testLayer = new Data.Layers.VectorLayer();
            testLayer.Name = "테스트레이어";
            testLayer.Enabled = true;
            map.AddLayer(testLayer);
            
            if (map.Layers.Count != 1 || map.GetLayerByName("테스트레이어") == null)
            {
                Console.WriteLine("❌ 레이어 추가 실패");
                return false;
            }
            
            Console.WriteLine("✅ 모든 기본 기능 테스트 통과!");
            Console.WriteLine($"   - 지도 크기: {map.Size.Width}x{map.Size.Height}");
            Console.WriteLine($"   - 중심좌표: ({map.Center.X:F6}, {map.Center.Y:F6})");
            Console.WriteLine($"   - 줌 레벨: {map.Zoom}");
            Console.WriteLine($"   - 좌표계: EPSG:{map.SRID}");
            Console.WriteLine($"   - 레이어 수: {map.Layers.Count}");
            
            // 정리
            map.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 테스트 중 예외 발생: {ex.Message}");
            return false;
        }
    }
    
    #endregion
}

/// <summary>
/// 레이어 이벤트 인자
/// </summary>
public class LayerEventArgs : EventArgs
{
    public ILayer Layer { get; }
    
    public LayerEventArgs(ILayer layer)
    {
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
    }
}