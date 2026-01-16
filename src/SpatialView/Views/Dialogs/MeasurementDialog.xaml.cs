using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SpatialView.Engine.Analysis;
using SpatialView.Engine.Geometry;

namespace SpatialView.Views.Dialogs;

/// <summary>
/// 측정 다이얼로그
/// </summary>
public partial class MeasurementDialog : Window
{
    private readonly MeasurementTool _measurementTool;
    private readonly List<Coordinate> _points = new();
    private MeasurementTool.MeasurementUnit _currentDistanceUnit = MeasurementTool.MeasurementUnit.Meters;
    private MeasurementTool.MeasurementUnit _currentAreaUnit = MeasurementTool.MeasurementUnit.SquareMeters;
    
    public ObservableCollection<SegmentInfo> Segments { get; } = new();
    
    /// <summary>
    /// 측정 유형 (거리/면적)
    /// </summary>
    public bool IsMeasuringDistance => DistanceRadio?.IsChecked == true;
    
    /// <summary>
    /// 측정 시작 이벤트
    /// </summary>
    public event Action<bool>? MeasurementStarted;
    
    /// <summary>
    /// 측정 종료 이벤트
    /// </summary>
    public event Action? MeasurementEnded;
    
    /// <summary>
    /// 포인트 추가됨 이벤트 (지도에서 그리기용)
    /// </summary>
    public event Action<List<Coordinate>>? PointsChanged;

    public MeasurementDialog(int srid = 4326)
    {
        InitializeComponent();
        
        _measurementTool = new MeasurementTool(srid);
        SegmentList.ItemsSource = Segments;
        
        // 기본 단위 선택
        UnitComboBox.SelectedIndex = 0;
        
        Loaded += (s, e) => MeasurementStarted?.Invoke(IsMeasuringDistance);
        Closed += (s, e) => MeasurementEnded?.Invoke();
    }
    
    /// <summary>
    /// 포인트 추가 (지도에서 클릭 시 호출)
    /// </summary>
    public void AddPoint(double x, double y)
    {
        _points.Add(new Coordinate(x, y));
        UpdateMeasurement();
        PointsChanged?.Invoke(new List<Coordinate>(_points));
    }
    
    /// <summary>
    /// 마지막 포인트 제거
    /// </summary>
    public void RemoveLastPoint()
    {
        if (_points.Count > 0)
        {
            _points.RemoveAt(_points.Count - 1);
            UpdateMeasurement();
            PointsChanged?.Invoke(new List<Coordinate>(_points));
        }
    }
    
    /// <summary>
    /// 측정 결과 업데이트
    /// </summary>
    private void UpdateMeasurement()
    {
        if (IsMeasuringDistance)
        {
            UpdateDistanceMeasurement();
        }
        else
        {
            UpdateAreaMeasurement();
        }
    }
    
    /// <summary>
    /// 거리 측정 업데이트
    /// </summary>
    private void UpdateDistanceMeasurement()
    {
        Segments.Clear();
        
        if (_points.Count < 2)
        {
            TotalResultText.Text = "0.00 m";
            return;
        }
        
        var result = _measurementTool.MeasurePolylineDistance(_points, _currentDistanceUnit);
        TotalResultText.Text = result.FormattedValue;
        
        // 세그먼트별 거리
        var segmentResults = _measurementTool.GetSegmentDistances(_points, _currentDistanceUnit);
        for (int i = 0; i < segmentResults.Count; i++)
        {
            Segments.Add(new SegmentInfo
            {
                Index = i + 1,
                Distance = segmentResults[i].FormattedValue
            });
        }
    }
    
    /// <summary>
    /// 면적 측정 업데이트
    /// </summary>
    private void UpdateAreaMeasurement()
    {
        Segments.Clear();
        PointCountText.Text = $"꼭지점: {_points.Count}개";
        
        if (_points.Count < 3)
        {
            TotalResultText.Text = "0.00 m²";
            return;
        }
        
        var result = _measurementTool.MeasureArea(_points, _currentAreaUnit);
        TotalResultText.Text = result.FormattedValue;
    }
    
    /// <summary>
    /// 측정 유형 변경
    /// </summary>
    private void MeasurementType_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        
        // 단위 콤보박스 업데이트
        UnitComboBox.Items.Clear();
        
        if (IsMeasuringDistance)
        {
            UnitComboBox.Items.Add(new ComboBoxItem { Content = "미터 (m)", Tag = "Meters" });
            UnitComboBox.Items.Add(new ComboBoxItem { Content = "킬로미터 (km)", Tag = "Kilometers" });
            UnitComboBox.Items.Add(new ComboBoxItem { Content = "마일 (mi)", Tag = "Miles" });
            UnitComboBox.Items.Add(new ComboBoxItem { Content = "피트 (ft)", Tag = "Feet" });
        }
        else
        {
            UnitComboBox.Items.Add(new ComboBoxItem { Content = "제곱미터 (m²)", Tag = "SquareMeters" });
            UnitComboBox.Items.Add(new ComboBoxItem { Content = "제곱킬로미터 (km²)", Tag = "SquareKilometers" });
            UnitComboBox.Items.Add(new ComboBoxItem { Content = "헥타르 (ha)", Tag = "Hectares" });
            UnitComboBox.Items.Add(new ComboBoxItem { Content = "에이커 (ac)", Tag = "Acres" });
        }
        
        UnitComboBox.SelectedIndex = 0;
        
        // 포인트 초기화
        _points.Clear();
        Segments.Clear();
        TotalResultText.Text = IsMeasuringDistance ? "0.00 m" : "0.00 m²";
        PointCountText.Text = "꼭지점: 0개";
        
        MeasurementStarted?.Invoke(IsMeasuringDistance);
        PointsChanged?.Invoke(new List<Coordinate>());
    }
    
    /// <summary>
    /// 단위 변경
    /// </summary>
    private void UnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UnitComboBox.SelectedItem is ComboBoxItem item && item.Tag is string unitTag)
        {
            if (IsMeasuringDistance)
            {
                _currentDistanceUnit = unitTag switch
                {
                    "Meters" => MeasurementTool.MeasurementUnit.Meters,
                    "Kilometers" => MeasurementTool.MeasurementUnit.Kilometers,
                    "Miles" => MeasurementTool.MeasurementUnit.Miles,
                    "Feet" => MeasurementTool.MeasurementUnit.Feet,
                    _ => MeasurementTool.MeasurementUnit.Meters
                };
            }
            else
            {
                _currentAreaUnit = unitTag switch
                {
                    "SquareMeters" => MeasurementTool.MeasurementUnit.SquareMeters,
                    "SquareKilometers" => MeasurementTool.MeasurementUnit.SquareKilometers,
                    "Hectares" => MeasurementTool.MeasurementUnit.Hectares,
                    "Acres" => MeasurementTool.MeasurementUnit.Acres,
                    _ => MeasurementTool.MeasurementUnit.SquareMeters
                };
            }
            
            UpdateMeasurement();
        }
    }
    
    /// <summary>
    /// 초기화 버튼
    /// </summary>
    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _points.Clear();
        Segments.Clear();
        TotalResultText.Text = IsMeasuringDistance ? "0.00 m" : "0.00 m²";
        PointCountText.Text = "꼭지점: 0개";
        PointsChanged?.Invoke(new List<Coordinate>());
    }
    
    /// <summary>
    /// 닫기 버튼
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    
    /// <summary>
    /// 현재 포인트 목록 반환
    /// </summary>
    public List<Coordinate> GetPoints() => new(_points);
}

/// <summary>
/// 세그먼트 정보
/// </summary>
public class SegmentInfo
{
    public int Index { get; set; }
    public string Distance { get; set; } = string.Empty;
}
