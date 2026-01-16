using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Data;
using System.Threading.Tasks;
using System.Linq;
using SpatialView.Core.GisEngine;
using SpatialView.Engine.Geometry;

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

    [ObservableProperty]
    private bool _hasUnsavedChanges = false;

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
                    {
                        var value = feature.GetAttribute(col.ColumnName);
                        // null 값은 DBNull.Value로 처리
                        newRow[col.ColumnName] = value ?? DBNull.Value;
                    }
                    else
                    {
                        newRow[col.ColumnName] = DBNull.Value;
                    }
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
    /// 속성 테이블 저장 요청 이벤트
    /// </summary>
    public event Action? SaveTableRequested;

    /// <summary>
    /// 속성 테이블 내보내기 요청 이벤트
    /// </summary>
    public event Action? ExportTableRequested;

    [RelayCommand]
    private void SaveTable()
    {
        SaveTableRequested?.Invoke();
    }

    [RelayCommand]
    private void ExportTable()
    {
        ExportTableRequested?.Invoke();
    }

    /// <summary>
    /// 변경된 속성을 원본 데이터 소스에 저장
    /// </summary>
    public async Task<(bool Success, string Message)> SaveToDataSourceAsync()
    {
        // 선택 레이어가 없으면 테이블명 기반으로 복구 시도
        if (SelectedLayer == null)
        {
            SelectedLayer = ResolveSelectedLayer();
        }
        if (SelectedLayer == null)
        {
            return (false, "선택된 레이어가 없습니다");
        }

        if (!HasUnsavedChanges)
        {
            return (true, "저장할 변경사항이 없습니다");
        }

        try
        {
            IsLoading = true;
            StatusMessage = "데이터 저장 중...";

            // SpatialViewVectorLayerAdapter에서 데이터 소스 가져오기
            if (SelectedLayer.Layer is not Infrastructure.GisEngine.SpatialViewVectorLayerAdapter adapter)
            {
                return (false, "지원되지 않는 레이어 유형입니다");
            }

            var engineLayer = adapter.GetEngineLayer();
            var dataSource = engineLayer.DataSource;

            if (dataSource == null)
            {
                return (false, "데이터 소스를 찾을 수 없습니다");
            }

            // Shapefile인 경우 직접 처리
            if (dataSource is Engine.Data.Sources.ShapefileDataSource shpDataSource)
            {
                var features = engineLayer.GetFeatures((Envelope?)null).ToList();

                // 필드 구조 변경 감지: 피처의 필드와 AttributeTable의 필드 비교
                var needsRewrite = DetectFieldStructureChanges(shpDataSource, features);

                if (needsRewrite && AttributeTable != null)
                {
                    // 필드 구조 변경이 있으면 DBF 파일 전체 재작성
                    var fieldDefs = new List<Engine.Data.Sources.DbfFieldDefinition>();
                    foreach (System.Data.DataColumn col in AttributeTable.Columns)
                    {
                        if (col.ColumnName == "FID") continue;
                        fieldDefs.Add(Engine.Data.Sources.DbfFieldDefinition.FromType(col.ColumnName, col.DataType));
                    }

                    // 피처에 AttributeTable의 최신 데이터 반영
                    SyncAttributeTableToFeatures(features);

                    var success = await Task.Run(() => shpDataSource.RewriteDbf(features, fieldDefs));

                    if (success)
                    {
                        HasUnsavedChanges = false;
                        StatusMessage = $"{features.Count}개 피처 저장 완료 (필드 구조 변경됨)";
                        return (true, $"{features.Count}개 피처가 저장되었습니다 (필드 구조 포함)");
                    }
                    else
                    {
                        return (false, "DBF 파일 재작성 실패: 파일이 잠겨있거나 쓰기 권한이 없습니다");
                    }
                }
                else
                {
                    // 필드 구조 변경 없이 값만 업데이트
                    SyncAttributeTableToFeatures(features);
                    var updateCount = await shpDataSource.UpdateFeaturesAsync(shpDataSource.Name, features);

                    if (updateCount > 0)
                    {
                        HasUnsavedChanges = false;
                        StatusMessage = $"{updateCount}개 피처 저장 완료";
                        return (true, $"{updateCount}개 피처가 저장되었습니다");
                    }
                    else
                    {
                        return (false, "저장 실패: 파일이 잠겨있거나 쓰기 권한이 없습니다");
                    }
                }
            }

            // 일반 IDataSource 인터페이스를 통한 저장
            var dsInterface = dataSource as Engine.Data.Sources.IDataSource;
            if (dsInterface == null)
            {
                return (false, "데이터 소스가 쓰기를 지원하지 않습니다");
            }

            if (dsInterface.IsReadOnly)
            {
                return (false, "읽기 전용 데이터 소스입니다");
            }

            // 모든 피처 업데이트
            var allFeatures = engineLayer.GetFeatures((Envelope?)null).ToList();
            int successCount = 0;
            int failCount = 0;

            foreach (var feature in allFeatures)
            {
                var result = await dsInterface.UpdateFeatureAsync(engineLayer.Name, feature);
                if (result)
                    successCount++;
                else
                    failCount++;
            }

            if (successCount > 0)
            {
                HasUnsavedChanges = false;
                StatusMessage = $"{successCount}개 피처 저장 완료";
                return (true, $"{successCount}개 피처가 저장되었습니다" + (failCount > 0 ? $" ({failCount}개 실패)" : ""));
            }
            else
            {
                return (false, "저장된 피처가 없습니다");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"저장 실패: {ex.Message}";
            return (false, $"저장 중 오류 발생: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 필드 구조 변경 감지
    /// </summary>
    private bool DetectFieldStructureChanges(Engine.Data.Sources.ShapefileDataSource shpDataSource, List<Engine.Data.IFeature> features)
    {
        if (AttributeTable == null || features.Count == 0) return false;

        // AttributeTable의 필드 목록 (FID 제외)
        var tableFields = new HashSet<string>();
        foreach (System.Data.DataColumn col in AttributeTable.Columns)
        {
            if (col.ColumnName != "FID")
                tableFields.Add(col.ColumnName);
        }

        // 피처의 필드 목록
        var featureFields = new HashSet<string>();
        var firstFeature = features.FirstOrDefault();
        if (firstFeature != null)
        {
            foreach (var name in firstFeature.Attributes.AttributeNames)
            {
                if (name != "Geometry" && name != "_geom_")
                    featureFields.Add(name);
            }
        }

        // 필드 목록이 다르면 구조 변경
        return !tableFields.SetEquals(featureFields);
    }

    /// <summary>
    /// AttributeTable 데이터를 피처에 동기화
    /// </summary>
    private void SyncAttributeTableToFeatures(List<Engine.Data.IFeature> features)
    {
        if (AttributeTable == null) return;

        // FID 기준으로 피처 매핑
        var featureMap = features.ToDictionary(f => Convert.ToUInt32(f.Id), f => f);

        foreach (System.Data.DataRow row in AttributeTable.Rows)
        {
            if (!row.Table.Columns.Contains("FID")) continue;

            var fid = Convert.ToUInt32(row["FID"]);
            if (!featureMap.TryGetValue(fid, out var feature)) continue;

            // 모든 필드 동기화
            foreach (System.Data.DataColumn col in AttributeTable.Columns)
            {
                if (col.ColumnName == "FID") continue;

                var value = row[col.ColumnName];
                feature.Attributes[col.ColumnName] = value == DBNull.Value ? null : value;
            }
        }
    }

    /// <summary>
    /// 속성 테이블을 CSV 파일로 내보내기
    /// </summary>
    public bool ExportTableToCsv(string filePath)
    {
        if (AttributeTable == null || AttributeTable.Rows.Count == 0)
        {
            StatusMessage = "내보낼 데이터가 없습니다";
            return false;
        }

        try
        {
            using (var writer = new System.IO.StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                // 헤더 작성
                var headers = new List<string>();
                foreach (System.Data.DataColumn col in AttributeTable.Columns)
                {
                    headers.Add(EscapeCsvField(col.ColumnName));
                }
                writer.WriteLine(string.Join(",", headers));

                // 데이터 작성
                foreach (System.Data.DataRow row in AttributeTable.Rows)
                {
                    var values = new List<string>();
                    foreach (System.Data.DataColumn col in AttributeTable.Columns)
                    {
                        var value = row[col];
                        values.Add(value == DBNull.Value ? "" : EscapeCsvField(value?.ToString() ?? ""));
                    }
                    writer.WriteLine(string.Join(",", values));
                }
            }

            StatusMessage = $"테이블 내보내기 완료: {filePath}";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"내보내기 실패: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 속성 테이블을 Excel 호환 형식으로 내보내기 (탭 구분)
    /// </summary>
    public bool ExportTableToTsv(string filePath)
    {
        if (AttributeTable == null || AttributeTable.Rows.Count == 0)
        {
            StatusMessage = "내보낼 데이터가 없습니다";
            return false;
        }

        try
        {
            using (var writer = new System.IO.StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                // 헤더 작성
                var headers = new List<string>();
                foreach (System.Data.DataColumn col in AttributeTable.Columns)
                {
                    headers.Add(col.ColumnName);
                }
                writer.WriteLine(string.Join("\t", headers));

                // 데이터 작성
                foreach (System.Data.DataRow row in AttributeTable.Rows)
                {
                    var values = new List<string>();
                    foreach (System.Data.DataColumn col in AttributeTable.Columns)
                    {
                        var value = row[col];
                        values.Add(value == DBNull.Value ? "" : value?.ToString() ?? "");
                    }
                    writer.WriteLine(string.Join("\t", values));
                }
            }

            StatusMessage = $"테이블 내보내기 완료: {filePath}";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"내보내기 실패: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// CSV 필드 이스케이프 처리
    /// </summary>
    private string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";

        // 쉼표, 따옴표, 개행이 포함된 경우 따옴표로 감싸기
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
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

            // VectorLayer 피처에도 필드 추가 동기화
            SyncFieldToVectorLayer(fieldName, fieldType, defaultValue);

            StatusMessage = $"필드 '{fieldName}' 추가됨";
            HasUnsavedChanges = true;

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
            if (SelectedLayer == null)
            {
                SelectedLayer = ResolveSelectedLayer();
            }
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

            // VectorLayer 피처에서도 필드 삭제 동기화
            RemoveFieldFromVectorLayer(fieldName);

            // DataGrid 갱신을 위해 테이블 재설정
            var temp = AttributeTable;
            AttributeTable = null;
            AttributeTable = temp;

            StatusMessage = $"필드 '{fieldName}' 삭제됨";
            HasUnsavedChanges = true;

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
            HasUnsavedChanges = true;

            // 결과를 실제 레이어 피처에 반영 (메모리 캐시 반영)
            var layerVm = SelectedLayer;
            if (layerVm?.Layer is Infrastructure.GisEngine.SpatialViewVectorLayerAdapter adapter)
            {
                var engineLayer = adapter.GetEngineLayer();
                var features = engineLayer.GetFeatures((Envelope?)null).ToList();
                var rowCount = Math.Min(AttributeTable.Rows.Count, features.Count);
                for (var i = 0; i < rowCount; i++)
                {
                    var row = AttributeTable.Rows[i];
                    var feature = features[i];
                    foreach (DataColumn col in AttributeTable.Columns)
                    {
                        if (col.ColumnName == "FID") continue;
                        var val = row[col.ColumnName];
                        feature.Attributes[col.ColumnName] = val == DBNull.Value ? null : val;
                    }
                }
                engineLayer.InvalidateViewportCache();
                layerVm.RaiseLayerChanged();
            }
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

    /// <summary>
    /// VectorLayer 피처에 새 필드 추가 동기화
    /// </summary>
    private void SyncFieldToVectorLayer(string fieldName, Type fieldType, object? defaultValue)
    {
        try
        {
            if (SelectedLayer == null)
            {
                SelectedLayer = ResolveSelectedLayer();
            }
            if (SelectedLayer?.Layer is not Infrastructure.GisEngine.SpatialViewVectorLayerAdapter adapter)
                return;

            var engineLayer = adapter.GetEngineLayer();
            var features = engineLayer.GetFeatures((Envelope?)null).ToList();

            foreach (var feature in features)
            {
                // 피처의 Attributes에 새 필드 추가
                if (!feature.Attributes.AttributeNames.Contains(fieldName))
                {
                    feature.Attributes[fieldName] = defaultValue;
                }
            }

            engineLayer.InvalidateViewportCache();
            System.Diagnostics.Debug.WriteLine($"SyncFieldToVectorLayer: '{fieldName}' 필드 추가됨 ({features.Count}개 피처)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SyncFieldToVectorLayer 오류: {ex.Message}");
        }
    }

    /// <summary>
    /// VectorLayer 피처에서 필드 삭제 동기화
    /// </summary>
    private void RemoveFieldFromVectorLayer(string fieldName)
    {
        try
        {
            if (SelectedLayer == null)
            {
                SelectedLayer = ResolveSelectedLayer();
            }
            if (SelectedLayer?.Layer is not Infrastructure.GisEngine.SpatialViewVectorLayerAdapter adapter)
                return;

            var engineLayer = adapter.GetEngineLayer();
            var features = engineLayer.GetFeatures((Envelope?)null).ToList();

            foreach (var feature in features)
            {
                // 피처의 Attributes에서 필드 삭제
                feature.Attributes.Remove(fieldName);
            }

            engineLayer.InvalidateViewportCache();
            System.Diagnostics.Debug.WriteLine($"RemoveFieldFromVectorLayer: '{fieldName}' 필드 삭제됨 ({features.Count}개 피처)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RemoveFieldFromVectorLayer 오류: {ex.Message}");
        }
    }

    private LayerItemViewModel? ResolveSelectedLayer()
    {
        if (Layers == null || Layers.Count == 0)
            return null;

        if (AttributeTable != null && !string.IsNullOrWhiteSpace(AttributeTable.TableName))
        {
            var byName = Layers.FirstOrDefault(l => l.Name == AttributeTable.TableName);
            if (byName != null)
                return byName;
        }

        return Layers.FirstOrDefault(l => !l.IsEmpty) ?? Layers.FirstOrDefault();
    }
}

