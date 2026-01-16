using System.Windows;
using System.Windows.Controls;
using SpatialView.Engine.Data.Layers;
using SpatialView.Engine.Editing;
using SpatialView.ViewModels;

namespace SpatialView.Views.Dialogs;

/// <summary>
/// 편집 도구 다이얼로그
/// </summary>
public partial class EditToolsDialog : Window
{
    private readonly List<LayerItemViewModel> _layers;
    private FeatureEditor? _editor;
    private VectorLayer? _currentLayer;
    
    /// <summary>
    /// 편집 모드 변경 이벤트
    /// </summary>
    public event Action<EditMode, GeometryType?>? EditModeChanged;
    
    /// <summary>
    /// 편집 완료 이벤트
    /// </summary>
    public event Action? EditCompleted;

    public EditToolsDialog(List<LayerItemViewModel> layers)
    {
        InitializeComponent();
        
        _layers = layers ?? new List<LayerItemViewModel>();
        
        // 편집 가능한 레이어만 표시
        var editableLayers = _layers
            .Where(l => l.Layer != null && !l.IsEmpty)
            .ToList();
        
        LayerComboBox.ItemsSource = editableLayers;
        LayerComboBox.DisplayMemberPath = "Name";
        
        if (editableLayers.Count > 0)
        {
            LayerComboBox.SelectedIndex = 0;
        }
        
        UpdateStatus("편집할 레이어를 선택하세요.");
    }
    
    /// <summary>
    /// 레이어 선택 변경
    /// </summary>
    private void LayerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LayerComboBox.SelectedItem is LayerItemViewModel layerItem)
        {
            // VectorLayer 가져오기
            if (layerItem.Layer is Infrastructure.GisEngine.SpatialViewVectorLayerAdapter adapter)
            {
                _currentLayer = adapter.GetEngineLayer();
                _editor = new FeatureEditor(_currentLayer);
                
                // 이벤트 연결
                _editor.EditChanged += OnEditChanged;
                _editor.EditCompleted += OnEditCompleted;
                
                // 레이어 지오메트리 타입에 따라 버튼 활성화/비활성화
                UpdateCreateButtonsForGeometryType(layerItem.GeometryType);
                
                UpdateStatus($"'{layerItem.Name}' 레이어 편집 준비됨 ({GetGeometryTypeName(layerItem.GeometryType)})");
                UpdateUndoRedoButtons();
            }
        }
    }
    
    /// <summary>
    /// 레이어 지오메트리 타입에 따라 생성 버튼 활성화/비활성화
    /// </summary>
    private void UpdateCreateButtonsForGeometryType(Core.Enums.GeometryType geometryType)
    {
        // 포인트 타입
        bool isPointType = geometryType == Core.Enums.GeometryType.Point || 
                           geometryType == Core.Enums.GeometryType.MultiPoint;
        
        // 라인 타입
        bool isLineType = geometryType == Core.Enums.GeometryType.Line || 
                          geometryType == Core.Enums.GeometryType.LineString ||
                          geometryType == Core.Enums.GeometryType.MultiLineString;
        
        // 폴리곤 타입
        bool isPolygonType = geometryType == Core.Enums.GeometryType.Polygon || 
                             geometryType == Core.Enums.GeometryType.MultiPolygon;
        
        CreatePointButton.IsEnabled = isPointType;
        CreateLineButton.IsEnabled = isLineType;
        CreatePolygonButton.IsEnabled = isPolygonType;
        
        // 툴팁 업데이트
        CreatePointButton.ToolTip = isPointType ? "포인트 생성" : "이 레이어는 포인트 타입이 아닙니다";
        CreateLineButton.ToolTip = isLineType ? "라인 생성" : "이 레이어는 라인 타입이 아닙니다";
        CreatePolygonButton.ToolTip = isPolygonType ? "폴리곤 생성" : "이 레이어는 폴리곤 타입이 아닙니다";
    }
    
    /// <summary>
    /// 지오메트리 타입 이름 반환
    /// </summary>
    private string GetGeometryTypeName(Core.Enums.GeometryType geometryType)
    {
        return geometryType switch
        {
            Core.Enums.GeometryType.Point => "포인트",
            Core.Enums.GeometryType.MultiPoint => "멀티포인트",
            Core.Enums.GeometryType.Line or Core.Enums.GeometryType.LineString => "라인",
            Core.Enums.GeometryType.MultiLineString => "멀티라인",
            Core.Enums.GeometryType.Polygon => "폴리곤",
            Core.Enums.GeometryType.MultiPolygon => "멀티폴리곤",
            _ => "알 수 없음"
        };
    }
    
    /// <summary>
    /// 포인트 생성
    /// </summary>
    private void CreatePoint_Click(object sender, RoutedEventArgs e)
    {
        if (_editor == null)
        {
            System.Windows.MessageBox.Show("편집할 레이어를 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        _editor.StartCreate(GeometryType.Point);
        UpdateStatus("지도에서 클릭하여 포인트를 생성하세요.");
        EditModeChanged?.Invoke(EditMode.Create, GeometryType.Point);
    }
    
    /// <summary>
    /// 라인 생성
    /// </summary>
    private void CreateLine_Click(object sender, RoutedEventArgs e)
    {
        if (_editor == null)
        {
            System.Windows.MessageBox.Show("편집할 레이어를 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        _editor.StartCreate(GeometryType.LineString);
        UpdateStatus("지도에서 클릭하여 라인을 그리세요. 더블클릭으로 완료.");
        EditModeChanged?.Invoke(EditMode.Create, GeometryType.LineString);
    }
    
    /// <summary>
    /// 폴리곤 생성
    /// </summary>
    private void CreatePolygon_Click(object sender, RoutedEventArgs e)
    {
        if (_editor == null)
        {
            System.Windows.MessageBox.Show("편집할 레이어를 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        _editor.StartCreate(GeometryType.Polygon);
        UpdateStatus("지도에서 클릭하여 폴리곤을 그리세요. 더블클릭으로 완료.");
        EditModeChanged?.Invoke(EditMode.Create, GeometryType.Polygon);
    }
    
    /// <summary>
    /// 피처 수정
    /// </summary>
    private void Modify_Click(object sender, RoutedEventArgs e)
    {
        if (_editor == null)
        {
            System.Windows.MessageBox.Show("편집할 레이어를 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        UpdateStatus("수정할 피처를 클릭하세요.");
        EditModeChanged?.Invoke(EditMode.Modify, null);
    }
    
    /// <summary>
    /// 피처 이동
    /// </summary>
    private void Move_Click(object sender, RoutedEventArgs e)
    {
        if (_editor == null)
        {
            System.Windows.MessageBox.Show("편집할 레이어를 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        UpdateStatus("이동할 피처를 클릭한 후 드래그하세요.");
        EditModeChanged?.Invoke(EditMode.Modify, null);
    }
    
    /// <summary>
    /// 피처 삭제
    /// </summary>
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_editor == null)
        {
            System.Windows.MessageBox.Show("편집할 레이어를 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        UpdateStatus("삭제할 피처를 클릭하세요.");
        EditModeChanged?.Invoke(EditMode.Delete, null);
    }
    
    /// <summary>
    /// 실행 취소
    /// </summary>
    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        _editor?.Undo();
        UpdateUndoRedoButtons();
    }
    
    /// <summary>
    /// 다시 실행
    /// </summary>
    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        _editor?.Redo();
        UpdateUndoRedoButtons();
    }
    
    /// <summary>
    /// 편집 이벤트 처리
    /// </summary>
    private void OnEditChanged(EditEventArgs args)
    {
        Dispatcher.Invoke(() =>
        {
            var message = args.Type switch
            {
                EditEventType.CreateStarted => "피처 생성 시작됨",
                EditEventType.PointAdded => "포인트 추가됨",
                EditEventType.PointRemoved => "포인트 제거됨",
                EditEventType.CreateCompleted => "피처 생성 완료",
                EditEventType.CreateCancelled => "피처 생성 취소됨",
                EditEventType.ValidationFailed => $"유효성 검사 실패: {args.ErrorMessage}",
                EditEventType.ModifyStarted => "피처 수정 시작됨",
                EditEventType.VertexMoved => "버텍스 이동됨",
                EditEventType.FeatureMoved => "피처 이동됨",
                EditEventType.ModifyCompleted => "피처 수정 완료",
                EditEventType.ModifyCancelled => "피처 수정 취소됨",
                EditEventType.FeatureDeleted => "피처 삭제됨",
                EditEventType.Undone => "실행 취소됨",
                EditEventType.Redone => "다시 실행됨",
                _ => "편집 중..."
            };
            
            UpdateStatus(message);
            UpdateUndoRedoButtons();
        });
    }
    
    /// <summary>
    /// 편집 완료 이벤트 처리
    /// </summary>
    private void OnEditCompleted(Engine.Data.IFeature feature)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateStatus($"피처 #{feature.Id} 편집 완료");
            EditCompleted?.Invoke();
        });
    }
    
    /// <summary>
    /// 상태 업데이트
    /// </summary>
    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
    }
    
    /// <summary>
    /// 실행 취소/다시 실행 버튼 상태 업데이트
    /// </summary>
    private void UpdateUndoRedoButtons()
    {
        UndoButton.IsEnabled = _editor?.CanUndo ?? false;
        RedoButton.IsEnabled = _editor?.CanRedo ?? false;
    }
    
    /// <summary>
    /// 포인트 추가 (외부에서 호출)
    /// </summary>
    public void AddPoint(double x, double y)
    {
        _editor?.AddPoint(x, y);
    }
    
    /// <summary>
    /// 마지막 포인트 제거 (외부에서 호출)
    /// </summary>
    public void RemoveLastPoint()
    {
        _editor?.RemoveLastPoint();
    }
    
    /// <summary>
    /// 생성 완료 (외부에서 호출)
    /// </summary>
    public void CompleteCreate()
    {
        _editor?.CompleteCreate();
    }
    
    /// <summary>
    /// 생성 취소 (외부에서 호출)
    /// </summary>
    public void CancelCreate()
    {
        _editor?.CancelCreate();
    }
    
    /// <summary>
    /// 현재 편집기 반환
    /// </summary>
    public FeatureEditor? GetEditor() => _editor;
    
    /// <summary>
    /// 완료 버튼
    /// </summary>
    private void FinishButton_Click(object sender, RoutedEventArgs e)
    {
        // 진행 중인 편집 완료
        if (_editor?.CurrentMode == EditMode.Create)
        {
            _editor.CompleteCreate();
        }
        else if (_editor?.CurrentMode == EditMode.Modify)
        {
            _editor.CompleteModify();
        }
        
        EditModeChanged?.Invoke(EditMode.None, null);
        UpdateStatus("편집 완료");
    }
    
    /// <summary>
    /// 닫기 버튼
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // 진행 중인 편집 취소
        if (_editor?.CurrentMode == EditMode.Create)
        {
            _editor.CancelCreate();
        }
        else if (_editor?.CurrentMode == EditMode.Modify)
        {
            _editor.CancelModify();
        }
        
        EditModeChanged?.Invoke(EditMode.None, null);
        Close();
    }
}
