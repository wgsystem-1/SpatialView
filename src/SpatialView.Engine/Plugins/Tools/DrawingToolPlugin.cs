using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data;
using SpatialView.Engine.Data.Layers;

namespace SpatialView.Engine.Plugins.Tools;

/// <summary>
/// 도형 그리기 도구 플러그인
/// </summary>
public abstract class DrawingToolPlugin : BaseToolPlugin
{
    protected readonly List<ICoordinate> _drawingPoints = new();
    protected ILayer? _targetLayer;
    protected DrawingMode _drawingMode = DrawingMode.Single;
    protected bool _isDrawing;

    public ILayer? TargetLayer 
    { 
        get => _targetLayer;
        set => _targetLayer = value;
    }

    public DrawingMode DrawingMode
    {
        get => _drawingMode;
        set
        {
            if (_drawingMode != value)
            {
                _drawingMode = value;
                CancelDrawing();
            }
        }
    }

    public override bool OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButton.Left || e.WorldCoordinate == null)
            return false;

        if (!_isDrawing)
        {
            StartDrawing(e.WorldCoordinate);
        }
        else
        {
            AddPoint(e.WorldCoordinate);
        }

        e.Handled = true;
        return true;
    }

    public override bool OnMouseMove(MouseEventArgs e)
    {
        if (_isDrawing && e.WorldCoordinate != null && _drawingPoints.Count > 0)
        {
            UpdatePreview(e.WorldCoordinate);
            e.Handled = true;
            return true;
        }

        return false;
    }

    public override bool OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButton.Right && _isDrawing)
        {
            CompleteDrawing();
            e.Handled = true;
            return true;
        }

        return false;
    }

    public override bool OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                CancelDrawing();
                e.Handled = true;
                return true;

            case Key.Enter:
                if (_isDrawing)
                {
                    CompleteDrawing();
                    e.Handled = true;
                    return true;
                }
                break;

            case Key.Delete:
                if (_isDrawing && _drawingPoints.Count > 0)
                {
                    RemoveLastPoint();
                    e.Handled = true;
                    return true;
                }
                break;
        }

        return false;
    }

    protected virtual void StartDrawing(ICoordinate firstPoint)
    {
        _isDrawing = true;
        _drawingPoints.Clear();
        _drawingPoints.Add(firstPoint);
        
        OnDrawingStarted();
    }

    protected virtual void AddPoint(ICoordinate point)
    {
        _drawingPoints.Add(point);
        
        OnPointAdded(point);

        // 점이나 선의 경우 바로 완료 처리
        if (ShouldAutoComplete())
        {
            CompleteDrawing();
        }
    }

    protected virtual void RemoveLastPoint()
    {
        if (_drawingPoints.Count > 1)
        {
            _drawingPoints.RemoveAt(_drawingPoints.Count - 1);
            OnPointRemoved();
        }
    }

    protected virtual void CompleteDrawing()
    {
        if (!CanComplete())
        {
            CancelDrawing();
            return;
        }

        var geometry = CreateGeometry();
        if (geometry != null)
        {
            var feature = CreateFeature(geometry);
            if (feature != null)
            {
                SaveFeature(feature);
                
                OnDrawingCompleted(feature);

                if (_drawingMode == DrawingMode.Single)
                {
                    _isDrawing = false;
                    _drawingPoints.Clear();
                }
                else
                {
                    // 연속 모드에서는 바로 다음 도형 시작
                    _drawingPoints.Clear();
                }
            }
        }
    }

    protected virtual void CancelDrawing()
    {
        _isDrawing = false;
        _drawingPoints.Clear();
        
        OnDrawingCancelled();
    }

    protected virtual void UpdatePreview(ICoordinate currentPosition)
    {
        // 미리보기 업데이트
        var previewGeometry = CreatePreviewGeometry(currentPosition);
        if (previewGeometry != null)
        {
            OnPreviewUpdated(previewGeometry);
        }
    }

    protected abstract IGeometry? CreateGeometry();
    protected abstract IGeometry? CreatePreviewGeometry(ICoordinate currentPosition);
    protected abstract bool CanComplete();
    protected abstract bool ShouldAutoComplete();

    protected virtual IFeature? CreateFeature(IGeometry geometry)
    {
        var feature = new Feature(Guid.NewGuid().ToString(), geometry, new AttributeTable());

        // 기본 속성 추가
        feature.Attributes["created"] = DateTime.Now;
        feature.Attributes["created_by"] = Author;

        return feature;
    }

    protected virtual void SaveFeature(IFeature feature)
    {
        if (_targetLayer == null)
        {
            Log("대상 레이어가 지정되지 않았습니다.", LogLevel.Warning);
            return;
        }

        // 편집 이벤트 발생
        var beforeEdit = new Events.BeforeEditEvent(this, feature, _targetLayer, Events.EditOperation.Create);
        PublishEvent(beforeEdit);

        if (beforeEdit.IsCancelled)
        {
            Log($"피처 생성이 취소되었습니다: {beforeEdit.CancelReason}");
            return;
        }

        // 피처 추가
        bool success;
        try
        {
            _targetLayer.AddFeature(feature);
            success = true;

            // 편집 완료 이벤트
            PublishEvent(new Events.AfterEditEvent(this, feature, _targetLayer, 
                Events.EditOperation.Create, success));
        }
        catch (Exception ex)
        {
            success = false;
            Log($"피처 추가 실패: {ex.Message}");
            PublishEvent(new Events.AfterEditEvent(this, feature, _targetLayer, 
                Events.EditOperation.Create, false));
        }

        if (success)
        {
            Log($"피처가 생성되었습니다: {feature.Id}");
        }
        else
        {
            Log("피처 생성에 실패했습니다.", LogLevel.Error);
        }
    }

    protected virtual void OnDrawingStarted() { }
    protected virtual void OnPointAdded(ICoordinate point) { }
    protected virtual void OnPointRemoved() { }
    protected virtual void OnDrawingCompleted(IFeature feature) { }
    protected virtual void OnDrawingCancelled() { }
    protected virtual void OnPreviewUpdated(IGeometry previewGeometry) { }
}

/// <summary>
/// 그리기 모드
/// </summary>
public enum DrawingMode
{
    /// <summary>단일 도형</summary>
    Single,
    /// <summary>연속 도형</summary>
    Continuous
}

/// <summary>
/// 점 그리기 도구
/// </summary>
public class PointDrawingTool : DrawingToolPlugin
{
    public override string Id => "SpatialView.Tools.DrawPoint";
    public override string Name => "점 그리기";
    public override string Description => "점을 그리는 도구";
    public override Version Version => new Version(1, 0, 0, 0);
    public override string Author => "SpatialView Team";

    public override string ToolName => "점";
    public override string? ToolIcon => null;
    public override string ToolCategory => "그리기";

    protected override Task<bool> OnInitializeAsync(IPluginContext context)
    {
        Log("점 그리기 도구 초기화");
        return Task.FromResult(true);
    }

    protected override void OnActivate()
    {
        Log("점 그리기 도구 활성화");
    }

    protected override void OnDeactivate()
    {
        Log("점 그리기 도구 비활성화");
        CancelDrawing();
    }

    protected override IGeometry? CreateGeometry()
    {
        return _drawingPoints.Count > 0 ? new Point(_drawingPoints[0]) : null;
    }

    protected override IGeometry? CreatePreviewGeometry(ICoordinate currentPosition)
    {
        return new Point(currentPosition);
    }

    protected override bool CanComplete()
    {
        return _drawingPoints.Count >= 1;
    }

    protected override bool ShouldAutoComplete()
    {
        return _drawingPoints.Count >= 1;
    }
}

/// <summary>
/// 선 그리기 도구
/// </summary>
public class LineDrawingTool : DrawingToolPlugin
{
    public override string Id => "SpatialView.Tools.DrawLine";
    public override string Name => "선 그리기";
    public override string Description => "선을 그리는 도구";
    public override Version Version => new Version(1, 0, 0, 0);
    public override string Author => "SpatialView Team";

    public override string ToolName => "선";
    public override string? ToolIcon => null;
    public override string ToolCategory => "그리기";

    protected override Task<bool> OnInitializeAsync(IPluginContext context)
    {
        Log("선 그리기 도구 초기화");
        return Task.FromResult(true);
    }

    protected override void OnActivate()
    {
        Log("선 그리기 도구 활성화");
    }

    protected override void OnDeactivate()
    {
        Log("선 그리기 도구 비활성화");
        CancelDrawing();
    }

    protected override IGeometry? CreateGeometry()
    {
        return _drawingPoints.Count >= 2 ? new LineString(_drawingPoints) : null;
    }

    protected override IGeometry? CreatePreviewGeometry(ICoordinate currentPosition)
    {
        if (_drawingPoints.Count == 0)
            return null;

        var previewPoints = new List<ICoordinate>(_drawingPoints) { currentPosition };
        return new LineString(previewPoints);
    }

    protected override bool CanComplete()
    {
        return _drawingPoints.Count >= 2;
    }

    protected override bool ShouldAutoComplete()
    {
        return false; // 선은 여러 점으로 구성 가능
    }
}

/// <summary>
/// 폴리곤 그리기 도구
/// </summary>
public class PolygonDrawingTool : DrawingToolPlugin
{
    public override string Id => "SpatialView.Tools.DrawPolygon";
    public override string Name => "폴리곤 그리기";
    public override string Description => "폴리곤을 그리는 도구";
    public override Version Version => new Version(1, 0, 0, 0);
    public override string Author => "SpatialView Team";

    public override string ToolName => "폴리곤";
    public override string? ToolIcon => null;
    public override string ToolCategory => "그리기";

    protected override Task<bool> OnInitializeAsync(IPluginContext context)
    {
        Log("폴리곤 그리기 도구 초기화");
        return Task.FromResult(true);
    }

    protected override void OnActivate()
    {
        Log("폴리곤 그리기 도구 활성화");
    }

    protected override void OnDeactivate()
    {
        Log("폴리곤 그리기 도구 비활성화");
        CancelDrawing();
    }

    protected override IGeometry? CreateGeometry()
    {
        if (_drawingPoints.Count < 3)
            return null;

        // 폴리곤은 닫힌 링이어야 함
        var ring = new LinearRing(_drawingPoints);
        return new Polygon(ring);
    }

    protected override IGeometry? CreatePreviewGeometry(ICoordinate currentPosition)
    {
        if (_drawingPoints.Count < 2)
            return null;

        var previewPoints = new List<ICoordinate>(_drawingPoints) { currentPosition };
        
        // 최소 3개 점이 있어야 폴리곤 생성 가능
        if (previewPoints.Count >= 3)
        {
            var ring = new LinearRing(previewPoints);
            return new Polygon(ring);
        }
        else
        {
            // 3개 미만일 때는 선으로 표시
            return new LineString(previewPoints);
        }
    }

    protected override bool CanComplete()
    {
        return _drawingPoints.Count >= 3;
    }

    protected override bool ShouldAutoComplete()
    {
        return false; // 폴리곤은 여러 점으로 구성
    }
}

/// <summary>
/// 사각형 그리기 도구
/// </summary>
public class RectangleDrawingTool : DrawingToolPlugin
{
    private ICoordinate? _startPoint;

    public override string Id => "SpatialView.Tools.DrawRectangle";
    public override string Name => "사각형 그리기";
    public override string Description => "사각형을 그리는 도구";
    public override Version Version => new Version(1, 0, 0, 0);
    public override string Author => "SpatialView Team";

    public override string ToolName => "사각형";
    public override string? ToolIcon => null;
    public override string ToolCategory => "그리기";

    protected override Task<bool> OnInitializeAsync(IPluginContext context)
    {
        Log("사각형 그리기 도구 초기화");
        return Task.FromResult(true);
    }

    protected override void OnActivate()
    {
        Log("사각형 그리기 도구 활성화");
    }

    protected override void OnDeactivate()
    {
        Log("사각형 그리기 도구 비활성화");
        CancelDrawing();
    }

    protected override void StartDrawing(ICoordinate firstPoint)
    {
        _startPoint = firstPoint;
        _isDrawing = true;
        _drawingPoints.Clear();
        
        OnDrawingStarted();
    }

    protected override IGeometry? CreateGeometry()
    {
        if (_startPoint == null || _drawingPoints.Count == 0)
            return null;

        return CreateRectangle(_startPoint, _drawingPoints[0]);
    }

    protected override IGeometry? CreatePreviewGeometry(ICoordinate currentPosition)
    {
        if (_startPoint == null)
            return null;

        return CreateRectangle(_startPoint, currentPosition);
    }

    private static Polygon CreateRectangle(ICoordinate p1, ICoordinate p2)
    {
        var minX = Math.Min(p1.X, p2.X);
        var minY = Math.Min(p1.Y, p2.Y);
        var maxX = Math.Max(p1.X, p2.X);
        var maxY = Math.Max(p1.Y, p2.Y);

        var coords = new List<ICoordinate>
        {
            new Coordinate(minX, minY),
            new Coordinate(maxX, minY),
            new Coordinate(maxX, maxY),
            new Coordinate(minX, maxY),
            new Coordinate(minX, minY) // 닫기
        };

        return new Polygon(new LinearRing(coords));
    }

    protected override bool CanComplete()
    {
        return _startPoint != null;
    }

    protected override bool ShouldAutoComplete()
    {
        return _drawingPoints.Count >= 1;
    }

    protected override void CancelDrawing()
    {
        base.CancelDrawing();
        _startPoint = null;
    }
}