using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Data;
using System.Threading.Tasks;
using System.Linq;
using SpatialView.Core.GisEngine;

namespace SpatialView.ViewModels;

/// <summary>
/// 속성 테이블 패널의 ViewModel
/// </summary>
public partial class AttributePanelViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<LayerItemViewModel> _layers = new();
    
    [ObservableProperty]
    private LayerItemViewModel? _selectedLayer;
    
    [ObservableProperty]
    private DataTable? _attributeTable;
    
    [ObservableProperty]
    private DataRowView? _selectedRow;
    
    [ObservableProperty]
    private string _filterText = string.Empty;
    
    [ObservableProperty]
    private int _featureCount = 0;
    
    [ObservableProperty]
    private int _selectedCount = 0;
    
    [ObservableProperty]
    private bool _isLoading = false;
    
    [ObservableProperty]
    private string _statusMessage = "레이어를 선택하세요";
    
    /// <summary>
    /// 선택된 피처 ID 목록
    /// </summary>
    public ObservableCollection<uint> SelectedFeatureIds { get; } = new();
    
    /// <summary>
    /// Feature 선택 변경 이벤트 (Map Highlight용)
    /// </summary>
    public event Action<IEnumerable<uint>>? FeatureSelectionChanged;
    
    /// <summary>
    /// Feature로 줌 요청 이벤트
    /// </summary>
    public event Action<uint>? ZoomToFeatureRequested;
    
    /// <summary>
    /// 레이어 목록 설정
    /// </summary>
    public void SetLayers(ObservableCollection<LayerItemViewModel> layers)
    {
        Layers = layers;
        
        // 첫 번째 레이어 선택
        if (Layers.Count > 0 && SelectedLayer == null)
        {
            SelectedLayer = Layers.FirstOrDefault(l => !l.IsEmpty);
        }
    }
    
    /// <summary>
    /// 선택된 레이어가 변경되면 속성 테이블 로드
    /// </summary>
    partial void OnSelectedLayerChanged(LayerItemViewModel? value)
    {
        if (value != null)
        {
#pragma warning disable CS4014 // fire-and-forget 허용
            _ = LoadAttributeTableAsync(value);
#pragma warning restore CS4014
        }
        else
        {
            AttributeTable = null;
            FeatureCount = 0;
            StatusMessage = "레이어를 선택하세요";
        }
    }
    
    /// <summary>
    /// 속성 테이블 로드
    /// </summary>
    private async Task LoadAttributeTableAsync(LayerItemViewModel layerItem)
    {
        if (layerItem.Layer is not IVectorLayer vectorLayer)
        {
            StatusMessage = "벡터 레이어만 속성 조회 가능합니다";
            AttributeTable = null;
            FeatureCount = 0;
            return;
        }
        
        IsLoading = true;
        StatusMessage = "속성 로딩 중...";
        
        try
        {
            var dataTable = await Task.Run(() => LoadFeaturesFromLayer(vectorLayer));
            
            AttributeTable = dataTable;
            FeatureCount = dataTable?.Rows.Count ?? 0;
            StatusMessage = $"{FeatureCount:N0}개 피처";
            
            SelectedFeatureIds.Clear();
            SelectedCount = 0;
        }
        catch (Exception ex)
        {
            StatusMessage = $"속성 로드 실패: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"속성 테이블 로드 오류: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 외부 호출 시 속성 테이블이 없으면 동기화 로드
    /// </summary>
    public async Task<bool> EnsureAttributeTableAsync(LayerItemViewModel layerItem)
    {
        // 이미 같은 레이어가 로드되어 있으면 재사용
        if (AttributeTable != null && SelectedLayer == layerItem)
            return true;

        SelectedLayer = layerItem;
        await LoadAttributeTableAsync(layerItem);
        return AttributeTable != null;
    }
    
    /// <summary>
    /// VectorLayer에서 피처 데이터 로드
    /// </summary>
    private DataTable? LoadFeaturesFromLayer(IVectorLayer vectorLayer)
    {
        if (vectorLayer.DataSource == null)
            return null;

        try
        {
            // IDataProvider로 접근해 실제 피처 ID(FID)를 사용하여 테이블 생성
            if (vectorLayer.DataSource is null)
                return null;
                
            var provider = vectorLayer.DataSource;

            provider.Open();

            var extent = provider.GetExtents();
            
            IEnumerable<uint> objectIds;
            if (extent == null || extent.IsNull)
            {
                // 범위가 없으면 전체 피처를 대상으로 한다.
                objectIds = provider.GetAllFeatures().Select(f => f.ID);
            }
            else
            {
                objectIds = provider.GetObjectIDsInView(extent);
                // 가시 범위 질의가 비어 있으면 전체로 폴백
                if (objectIds == null || !objectIds.Any())
                    objectIds = provider.GetAllFeatures().Select(f => f.ID);
            }

            DataTable? resultTable = null;
            bool schemaInitialized = false;

            foreach (var oid in objectIds)
            {
                var feature = provider.GetFeature(oid);
                if (feature == null)
                    continue;

                if (!schemaInitialized)
                {
                    resultTable = new DataTable(vectorLayer.Name);

                    // FID 컬럼
                    if (!resultTable.Columns.Contains("FID"))
                        resultTable.Columns.Add("FID", typeof(uint));

                    // 속성 컬럼 (Geometry 제외)
                    foreach (var attributeName in feature.AttributeNames)
                    {
                        if (attributeName == "Geometry" || attributeName == "_geom_")
                            continue;
                        if (!resultTable.Columns.Contains(attributeName))
                        {
                            var value = feature.GetAttribute(attributeName);
                            var dataType = value?.GetType() ?? typeof(object);
                            resultTable.Columns.Add(attributeName, dataType);
                        }
                    }

                    schemaInitialized = true;
                }

                if (resultTable == null)
                    continue;

                var newRow = resultTable.NewRow();
                newRow["FID"] = oid;

                foreach (DataColumn col in resultTable.Columns)
                {
                    if (col.ColumnName == "FID")
                        continue;

                    if (feature.AttributeNames.Contains(col.ColumnName))
                        newRow[col.ColumnName] = feature.GetAttribute(col.ColumnName);
                }

                resultTable.Rows.Add(newRow);
            }

            provider.Close();

            return resultTable;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"피처 로드 오류: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 필터 텍스트 변경 시 필터 적용
    /// </summary>
    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter(value);
    }
    
    /// <summary>
    /// 필터 적용
    /// </summary>
    private void ApplyFilter(string filterText)
    {
        if (AttributeTable == null) return;
        
        try
        {
            if (string.IsNullOrWhiteSpace(filterText))
            {
                AttributeTable.DefaultView.RowFilter = string.Empty;
            }
            else
            {
                // 모든 문자열 컬럼에서 검색
                var conditions = new List<string>();
                foreach (DataColumn col in AttributeTable.Columns)
                {
                    if (col.DataType == typeof(string))
                    {
                        conditions.Add($"[{col.ColumnName}] LIKE '%{filterText.Replace("'", "''")}%'");
                    }
                }
                
                if (conditions.Count > 0)
                {
                    AttributeTable.DefaultView.RowFilter = string.Join(" OR ", conditions);
                }
            }
            
            FeatureCount = AttributeTable.DefaultView.Count;
            StatusMessage = $"{FeatureCount:N0}개 피처 (필터됨)";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"필터 적용 오류: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 행 선택 시 처리
    /// </summary>
    partial void OnSelectedRowChanged(DataRowView? value)
    {
        if (value != null && value.Row.Table.Columns.Contains("FID"))
        {
            var fid = Convert.ToUInt32(value["FID"]);
            SelectedFeatureIds.Clear();
            SelectedFeatureIds.Add(fid);
            SelectedCount = 1;
            
            FeatureSelectionChanged?.Invoke(SelectedFeatureIds);
        }
    }
    
    [RelayCommand]
    private void ClearFilter()
    {
        FilterText = string.Empty;
    }
    
    [RelayCommand]
    private void ZoomToSelected()
    {
        if (SelectedRow != null && SelectedRow.Row.Table.Columns.Contains("FID"))
        {
            var fid = Convert.ToUInt32(SelectedRow["FID"]);
            ZoomToFeatureRequested?.Invoke(fid);
        }
    }
    
    [RelayCommand]
    private void ClearSelection()
    {
        SelectedFeatureIds.Clear();
        SelectedCount = 0;
        SelectedRow = null;
        FeatureSelectionChanged?.Invoke(Enumerable.Empty<uint>());
    }
    
    [RelayCommand]
    private void Refresh()
    {
        if (SelectedLayer != null)
        {
            _ = LoadAttributeTableAsync(SelectedLayer);
        }
    }
    
    /// <summary>
    /// 필드 추가 다이얼로그 열기 요청 이벤트
    /// </summary>
    public event Action? AddFieldRequested;
    
    /// <summary>
    /// 필드 삭제 요청 이벤트
    /// </summary>
    public event Action<string>? DeleteFieldRequested;
    
    /// <summary>
    /// 필드 계산기 요청 이벤트
    /// </summary>
    public event Action? FieldCalculatorRequested;
    
    [RelayCommand]
    private void AddField()
    {
        AddFieldRequested?.Invoke();
    }

    /// <summary>
    /// FID로 행 선택 시도
    /// </summary>
    public bool TrySelectFid(uint fid)
    {
        if (AttributeTable == null || !AttributeTable.Columns.Contains("FID"))
            return false;

        var rowView = AttributeTable.DefaultView.Cast<DataRowView>()
            .FirstOrDefault(r => Convert.ToUInt32(r["FID"]) == fid);

        if (rowView != null)
        {
            SelectedRow = rowView;
            return true;
        }

        return false;
    }
    
    [RelayCommand]
    private void DeleteField()
    {
        // 선택된 열 삭제는 ContextMenu에서 처리
        // 현재는 기본 동작으로 마지막 추가된 사용자 필드 삭제
        if (AttributeTable != null && AttributeTable.Columns.Count > 1)
        {
            // FID를 제외한 마지막 열 찾기
            var lastColumn = AttributeTable.Columns[AttributeTable.Columns.Count - 1];
            if (lastColumn.ColumnName != "FID")
            {
                DeleteFieldRequested?.Invoke(lastColumn.ColumnName);
            }
        }
    }
    
    [RelayCommand]
    private void OpenFieldCalculator()
    {
        FieldCalculatorRequested?.Invoke();
    }
    
    /// <summary>
    /// 필드 추가 실행
    /// </summary>
    public void ExecuteAddField(string fieldName, Type fieldType, object? defaultValue)
    {
        if (AttributeTable == null) return;
        
        try
        {
            // 중복 확인
            if (AttributeTable.Columns.Contains(fieldName))
            {
                StatusMessage = $"필드 '{fieldName}'이(가) 이미 존재합니다";
                return;
            }
            
            // 열 추가
            var newColumn = new DataColumn(fieldName, fieldType);
            AttributeTable.Columns.Add(newColumn);
            
            // 기본값 설정
            if (defaultValue != null)
            {
                foreach (DataRow row in AttributeTable.Rows)
                {
                    try
                    {
                        row[fieldName] = Convert.ChangeType(defaultValue, fieldType);
                    }
                    catch
                    {
                        row[fieldName] = DBNull.Value;
                    }
                }
            }
            
            StatusMessage = $"필드 '{fieldName}' 추가됨";
            
            // DataGrid 갱신을 위해 PropertyChanged 발생
            OnPropertyChanged(nameof(AttributeTable));
        }
        catch (Exception ex)
        {
            StatusMessage = $"필드 추가 실패: {ex.Message}";
        }
    }
    
    /// <summary>
    /// 필드 삭제 실행
    /// </summary>
    public void ExecuteDeleteField(string fieldName)
    {
        if (AttributeTable == null) return;
        
        try
        {
            if (fieldName == "FID")
            {
                StatusMessage = "FID 필드는 삭제할 수 없습니다";
                System.Windows.MessageBox.Show("FID 필드는 삭제할 수 없습니다.", "알림",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            
            if (!AttributeTable.Columns.Contains(fieldName))
            {
                StatusMessage = $"필드 '{fieldName}'을(를) 찾을 수 없습니다";
                System.Windows.MessageBox.Show($"필드 '{fieldName}'을(를) 찾을 수 없습니다.", "오류",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
            
            // 열 삭제
            AttributeTable.Columns.Remove(fieldName);
            
            // DataGrid 갱신을 위해 테이블 재설정
            var temp = AttributeTable;
            AttributeTable = null;
            AttributeTable = temp;
            
            StatusMessage = $"필드 '{fieldName}' 삭제됨";
            System.Windows.MessageBox.Show($"필드 '{fieldName}'이(가) 삭제되었습니다.", "완료",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"필드 삭제 실패: {ex.Message}";
            System.Windows.MessageBox.Show($"필드 삭제 실패: {ex.Message}", "오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
    
    /// <summary>
    /// 필드 계산 실행
    /// </summary>
    public void ExecuteFieldCalculation(string targetField, string expression, 
        bool isNewField = false, string? newFieldName = null, Type? newFieldType = null)
    {
        if (AttributeTable == null) return;
        
        try
        {
            // 새 필드 생성
            if (isNewField && !string.IsNullOrEmpty(newFieldName) && newFieldType != null)
            {
                if (!AttributeTable.Columns.Contains(newFieldName))
                {
                    AttributeTable.Columns.Add(newFieldName, newFieldType);
                }
                targetField = newFieldName;
            }
            
            if (!AttributeTable.Columns.Contains(targetField))
            {
                StatusMessage = $"대상 필드 '{targetField}'을(를) 찾을 수 없습니다";
                return;
            }
            
            var successCount = 0;
            var errorCount = 0;
            
            foreach (DataRow row in AttributeTable.Rows)
            {
                try
                {
                    var result = Views.Dialogs.FieldCalculatorDialog.EvaluateExpression(row, expression);
                    if (result != null)
                    {
                        row[targetField] = Convert.ChangeType(result, 
                            AttributeTable.Columns[targetField]!.DataType);
                        successCount++;
                    }
                }
                catch
                {
                    errorCount++;
                }
            }
            
            StatusMessage = $"계산 완료: {successCount}개 성공, {errorCount}개 실패";
            OnPropertyChanged(nameof(AttributeTable));
        }
        catch (Exception ex)
        {
            StatusMessage = $"계산 실패: {ex.Message}";
        }
    }
    
    /// <summary>
    /// 피처 ID 목록으로 피처 선택 (지도에서 선택 시 호출)
    /// </summary>
    public void SelectFeaturesByIds(List<uint> featureIds)
    {
        SelectedFeatureIds.Clear();
        
        if (featureIds == null || featureIds.Count == 0)
        {
            SelectedCount = 0;
            SelectedRow = null;
            return;
        }
        
        foreach (var id in featureIds)
        {
            SelectedFeatureIds.Add(id);
        }
        
        SelectedCount = featureIds.Count;
        
        // DataTable에서 해당 행 선택 (첫 번째 피처)
        if (AttributeTable != null && featureIds.Count > 0)
        {
            var firstId = featureIds[0];
            foreach (DataRowView row in AttributeTable.DefaultView)
            {
                if (row.Row.Table.Columns.Contains("FID"))
                {
                    var fid = Convert.ToUInt32(row["FID"]);
                    if (fid == firstId)
                    {
                        SelectedRow = row;
                        break;
                    }
                }
            }
        }
        
        StatusMessage = $"{featureIds.Count}개 피처 선택됨";
    }
}

