using SpatialView.Engine.Plugins.Tools;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Analysis;

namespace SpatialView.Engine.Plugins.Samples;

/// <summary>
/// 측정 도구 플러그인
/// </summary>
public class MeasureToolPlugin : BaseToolPlugin
{
    private readonly List<ICoordinate> _measurePoints = new();
    private MeasureMode _mode = MeasureMode.Distance;
    private Events.IEventSubscription? _viewChangedSubscription;

    public override string Id => "SpatialView.Tools.Measure";
    public override string Name => "측정 도구";
    public override string Description => "거리와 면적을 측정하는 도구";
    public override Version Version => new Version(1, 0, 0, 0);
    public override string Author => "SpatialView Team";

    public override string ToolName => "측정";
    public override string? ToolIcon => null;
    public override string ToolCategory => "분석";

    public MeasureMode Mode 
    { 
        get => _mode;
        set
        {
            if (_mode != value)
            {
                _mode = value;
                ClearMeasurement();
            }
        }
    }

    protected override Task<bool> OnInitializeAsync(IPluginContext context)
    {
        Log("측정 도구 초기화 중...");
        
        // 뷰 변경 시 측정 결과 업데이트
        _viewChangedSubscription = SubscribeEvent<Events.ViewChangedEvent>(OnViewChanged);
        
        return Task.FromResult(true);
    }

    protected override void OnActivate()
    {
        Log("측정 도구 활성화");
        ClearMeasurement();
    }

    protected override void OnDeactivate()
    {
        Log("측정 도구 비활성화");
        ClearMeasurement();
    }

    public override bool OnMouseDown(MouseEventArgs e)
    {
        if (e.WorldCoordinate == null)
            return false;

        switch (e.Button)
        {
            case MouseButton.Left:
                AddMeasurePoint(e.WorldCoordinate);
                e.Handled = true;
                return true;

            case MouseButton.Right:
                CompleteMeasurement();
                e.Handled = true;
                return true;
        }

        return false;
    }

    public override bool OnMouseMove(MouseEventArgs e)
    {
        if (_measurePoints.Count > 0 && e.WorldCoordinate != null)
        {
            // 임시 측정 결과 표시
            UpdateTemporaryMeasurement(e.WorldCoordinate);
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
                ClearMeasurement();
                e.Handled = true;
                return true;

            case Key.Enter:
                CompleteMeasurement();
                e.Handled = true;
                return true;
        }

        return false;
    }

    private void AddMeasurePoint(ICoordinate coordinate)
    {
        _measurePoints.Add(coordinate);
        
        // 측정 그래픽 업데이트
        UpdateMeasurementGraphics();

        // 측정 결과 계산
        if (_mode == MeasureMode.Distance && _measurePoints.Count >= 2)
        {
            var distance = CalculateTotalDistance();
            PublishMeasureResult($"거리: {FormatDistance(distance)}");
        }
        else if (_mode == MeasureMode.Area && _measurePoints.Count >= 3)
        {
            var area = CalculateArea();
            PublishMeasureResult($"면적: {FormatArea(area)}");
        }
    }

    private void CompleteMeasurement()
    {
        if (_measurePoints.Count < 2)
        {
            ClearMeasurement();
            return;
        }

        // 최종 측정 결과
        if (_mode == MeasureMode.Distance)
        {
            var distance = CalculateTotalDistance();
            PublishMeasureResult($"총 거리: {FormatDistance(distance)}", true);
        }
        else if (_mode == MeasureMode.Area && _measurePoints.Count >= 3)
        {
            var area = CalculateArea();
            var perimeter = CalculatePerimeter();
            PublishMeasureResult($"면적: {FormatArea(area)}, 둘레: {FormatDistance(perimeter)}", true);
        }

        ClearMeasurement();
    }

    private void ClearMeasurement()
    {
        _measurePoints.Clear();
        UpdateMeasurementGraphics();
    }

    private double CalculateTotalDistance()
    {
        if (_measurePoints.Count < 2)
            return 0;

        double totalDistance = 0;
        for (int i = 1; i < _measurePoints.Count; i++)
        {
            totalDistance += _measurePoints[i - 1].Distance(_measurePoints[i]);
        }

        return totalDistance;
    }

    private double CalculateArea()
    {
        if (_measurePoints.Count < 3)
            return 0;

        // 폴리곤 생성 후 면적 계산
        var ring = new LinearRing(_measurePoints);
        var polygon = new Polygon(ring);
        return polygon.Area;
    }

    private double CalculatePerimeter()
    {
        if (_measurePoints.Count < 3)
            return 0;

        double perimeter = CalculateTotalDistance();
        
        // 마지막 점에서 첫 점까지의 거리 추가
        if (_measurePoints.Count > 2)
        {
            perimeter += _measurePoints.Last().Distance(_measurePoints.First());
        }

        return perimeter;
    }

    private void UpdateMeasurementGraphics()
    {
        // 맵 캔버스에 측정 그래픽 업데이트
        // 실제 구현에서는 오버레이 레이어나 그래픽 레이어 사용
        Log($"측정 점 개수: {_measurePoints.Count}", LogLevel.Debug);
    }

    private void UpdateTemporaryMeasurement(ICoordinate currentPosition)
    {
        // 마우스 위치까지의 임시 측정 표시
        if (_measurePoints.Count == 0)
            return;

        if (_mode == MeasureMode.Distance)
        {
            var lastPoint = _measurePoints.Last();
            var tempDistance = lastPoint.Distance(currentPosition);
            var totalDistance = CalculateTotalDistance() + tempDistance;
            
            PublishMeasureResult($"거리: {FormatDistance(totalDistance)} (임시)");
        }
        else if (_mode == MeasureMode.Area && _measurePoints.Count >= 2)
        {
            var tempPoints = new List<ICoordinate>(_measurePoints) { currentPosition };
            var ring = new LinearRing(tempPoints);
            var polygon = new Polygon(ring);
            
            PublishMeasureResult($"면적: {FormatArea(polygon.Area)} (임시)");
        }
    }

    private void OnViewChanged(Events.ViewChangedEvent e)
    {
        // 뷰 변경 시 측정 그래픽 업데이트
        if (_measurePoints.Count > 0)
        {
            UpdateMeasurementGraphics();
        }
    }

    private void PublishMeasureResult(string result, bool isFinal = false)
    {
        // 측정 결과 이벤트 발생
        PublishEvent(new MeasurementResultEvent(this, result, isFinal));
        
        Log(result);
    }

    private static string FormatDistance(double distance)
    {
        if (distance < 1)
            return $"{distance * 1000:F2} m";
        else if (distance < 10)
            return $"{distance:F3} km";
        else if (distance < 100)
            return $"{distance:F2} km";
        else
            return $"{distance:F1} km";
    }

    private static string FormatArea(double area)
    {
        if (area < 0.01) // 10,000 m²
            return $"{area * 1000000:F0} m²";
        else if (area < 1)
            return $"{area * 10000:F0} ha";
        else if (area < 100)
            return $"{area:F2} km²";
        else
            return $"{area:F0} km²";
    }

    protected override void OnDispose()
    {
        _viewChangedSubscription?.Dispose();
        base.OnDispose();
    }

    public override IPluginSettings GetSettings()
    {
        return new MeasureToolSettings
        {
            DefaultMode = _mode,
            LineColor = "#FF0000",
            FillColor = "#FF000033",
            LineWidth = 2
        };
    }

    public override void ApplySettings(IPluginSettings settings)
    {
        if (settings is MeasureToolSettings measureSettings)
        {
            _mode = measureSettings.DefaultMode;
            // 스타일 설정 적용
        }
    }
}

/// <summary>
/// 측정 모드
/// </summary>
public enum MeasureMode
{
    /// <summary>거리 측정</summary>
    Distance,
    /// <summary>면적 측정</summary>
    Area
}

/// <summary>
/// 측정 결과 이벤트
/// </summary>
public class MeasurementResultEvent : Events.EventBase
{
    public string Result { get; }
    public bool IsFinal { get; }

    public MeasurementResultEvent(object source, string result, bool isFinal)
        : base(source)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        IsFinal = isFinal;
    }
}

/// <summary>
/// 측정 도구 설정
/// </summary>
public class MeasureToolSettings : IPluginSettings
{
    public MeasureMode DefaultMode { get; set; }
    public string LineColor { get; set; } = "#FF0000";
    public string FillColor { get; set; } = "#FF000033";
    public int LineWidth { get; set; } = 2;

    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(this);
    }

    public void FromJson(string json)
    {
        var settings = System.Text.Json.JsonSerializer.Deserialize<MeasureToolSettings>(json);
        if (settings != null)
        {
            DefaultMode = settings.DefaultMode;
            LineColor = settings.LineColor;
            FillColor = settings.FillColor;
            LineWidth = settings.LineWidth;
        }
    }

    public void ResetToDefaults()
    {
        DefaultMode = MeasureMode.Distance;
        LineColor = "#FF0000";
        FillColor = "#FF000033";
        LineWidth = 2;
    }

    public bool Validate(out string? errorMessage)
    {
        errorMessage = null;
        
        if (LineWidth < 1 || LineWidth > 10)
        {
            errorMessage = "선 두께는 1-10 사이여야 합니다.";
            return false;
        }

        return true;
    }
}