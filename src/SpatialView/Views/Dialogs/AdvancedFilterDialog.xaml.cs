using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SpatialView.Views.Dialogs;

/// <summary>
/// 고급 필터 다이얼로그
/// </summary>
public partial class AdvancedFilterDialog : Window
{
    private readonly DataTable _dataTable;
    public ObservableCollection<FilterCondition> Conditions { get; } = new();
    
    /// <summary>
    /// 필터 적용 이벤트
    /// </summary>
    public event Action<string>? FilterApplied;
    
    /// <summary>
    /// 필터 결과 개수
    /// </summary>
    public int FilteredCount { get; private set; }

    public AdvancedFilterDialog(DataTable dataTable)
    {
        InitializeComponent();
        
        _dataTable = dataTable ?? throw new ArgumentNullException(nameof(dataTable));
        
        // 필드 목록 초기화
        var fields = new List<string>();
        foreach (DataColumn col in _dataTable.Columns)
        {
            fields.Add(col.ColumnName);
        }
        FieldComboBox.ItemsSource = fields;
        if (fields.Count > 0)
            FieldComboBox.SelectedIndex = 0;
        
        OperatorComboBox.SelectedIndex = 0;
        
        ConditionListBox.ItemsSource = Conditions;
        
        UpdateResultInfo();
    }
    
    /// <summary>
    /// 빠른 검색 실행
    /// </summary>
    private void QuickSearch_Click(object sender, RoutedEventArgs e)
    {
        ExecuteQuickSearch();
    }
    
    /// <summary>
    /// 빠른 검색 (Enter 키)
    /// </summary>
    private void QuickSearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteQuickSearch();
        }
    }
    
    /// <summary>
    /// 빠른 검색 실행
    /// </summary>
    private void ExecuteQuickSearch()
    {
        var searchText = QuickSearchTextBox.Text?.Trim();
        
        if (string.IsNullOrEmpty(searchText))
        {
            // 필터 해제
            _dataTable.DefaultView.RowFilter = string.Empty;
            FilteredCount = _dataTable.Rows.Count;
        }
        else
        {
            // 모든 문자열 컬럼에서 검색
            var conditions = new List<string>();
            foreach (DataColumn col in _dataTable.Columns)
            {
                if (col.DataType == typeof(string))
                {
                    conditions.Add($"[{col.ColumnName}] LIKE '%{EscapeFilterValue(searchText)}%'");
                }
            }
            
            if (conditions.Count > 0)
            {
                var filter = string.Join(" OR ", conditions);
                _dataTable.DefaultView.RowFilter = filter;
                FilteredCount = _dataTable.DefaultView.Count;
            }
        }
        
        UpdateResultInfo();
        FilterApplied?.Invoke(_dataTable.DefaultView.RowFilter);
    }
    
    /// <summary>
    /// 조건 추가
    /// </summary>
    private void AddCondition_Click(object sender, RoutedEventArgs e)
    {
        var field = FieldComboBox.SelectedItem as string;
        var operatorItem = OperatorComboBox.SelectedItem as ComboBoxItem;
        var value = ValueTextBox.Text?.Trim();
        
        if (string.IsNullOrEmpty(field) || operatorItem == null)
        {
            System.Windows.MessageBox.Show("필드와 연산자를 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        var op = operatorItem.Tag?.ToString() ?? "=";
        
        // NULL/NOT NULL 연산자는 값이 필요 없음
        if (op != "NULL" && op != "NOTNULL" && string.IsNullOrEmpty(value))
        {
            System.Windows.MessageBox.Show("값을 입력하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        var condition = new FilterCondition
        {
            Field = field,
            Operator = op,
            Value = value ?? string.Empty,
            LogicalOperator = Conditions.Count > 0 ? "AND" : "",
            ShowLogicalOperator = Conditions.Count > 0
        };
        
        Conditions.Add(condition);
        ValueTextBox.Clear();
        
        // 자동 적용
        ApplyFilter();
    }
    
    /// <summary>
    /// 조건 제거
    /// </summary>
    private void RemoveCondition_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is FilterCondition condition)
        {
            Conditions.Remove(condition);
            
            // 첫 번째 조건의 논리 연산자 숨기기
            if (Conditions.Count > 0)
            {
                Conditions[0].ShowLogicalOperator = false;
                Conditions[0].LogicalOperator = "";
            }
            
            ApplyFilter();
        }
    }
    
    /// <summary>
    /// 모든 조건 삭제
    /// </summary>
    private void ClearAllConditions_Click(object sender, RoutedEventArgs e)
    {
        Conditions.Clear();
        ApplyFilter();
    }
    
    /// <summary>
    /// 필터 적용
    /// </summary>
    private void ApplyFilter()
    {
        try
        {
            if (Conditions.Count == 0)
            {
                _dataTable.DefaultView.RowFilter = string.Empty;
                FilteredCount = _dataTable.Rows.Count;
            }
            else
            {
                var filterParts = new List<string>();
                
                foreach (var condition in Conditions)
                {
                    var filterPart = BuildFilterExpression(condition);
                    if (!string.IsNullOrEmpty(filterPart))
                    {
                        if (filterParts.Count > 0 && !string.IsNullOrEmpty(condition.LogicalOperator))
                        {
                            filterParts.Add(condition.LogicalOperator);
                        }
                        filterParts.Add($"({filterPart})");
                    }
                }
                
                var filter = string.Join(" ", filterParts);
                _dataTable.DefaultView.RowFilter = filter;
                FilteredCount = _dataTable.DefaultView.Count;
            }
            
            UpdateResultInfo();
            FilterApplied?.Invoke(_dataTable.DefaultView.RowFilter);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"필터 적용 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    /// <summary>
    /// 필터 표현식 생성
    /// </summary>
    private string BuildFilterExpression(FilterCondition condition)
    {
        var field = $"[{condition.Field}]";
        var value = EscapeFilterValue(condition.Value);
        
        return condition.Operator switch
        {
            "=" => $"{field} = '{value}'",
            "<>" => $"{field} <> '{value}'",
            ">" => $"{field} > '{value}'",
            ">=" => $"{field} >= '{value}'",
            "<" => $"{field} < '{value}'",
            "<=" => $"{field} <= '{value}'",
            "LIKE" => $"{field} LIKE '%{value}%'",
            "STARTS" => $"{field} LIKE '{value}%'",
            "ENDS" => $"{field} LIKE '%{value}'",
            "NULL" => $"{field} IS NULL",
            "NOTNULL" => $"{field} IS NOT NULL",
            _ => $"{field} = '{value}'"
        };
    }
    
    /// <summary>
    /// 필터 값 이스케이프
    /// </summary>
    private static string EscapeFilterValue(string value)
    {
        return value?.Replace("'", "''") ?? string.Empty;
    }
    
    /// <summary>
    /// 결과 정보 업데이트
    /// </summary>
    private void UpdateResultInfo()
    {
        FilteredCount = _dataTable.DefaultView.Count;
        ResultInfoText.Text = $"필터 결과: {FilteredCount:N0}개 피처 (전체 {_dataTable.Rows.Count:N0}개)";
    }
    
    /// <summary>
    /// 적용 버튼
    /// </summary>
    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }
    
    /// <summary>
    /// 초기화 버튼
    /// </summary>
    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        Conditions.Clear();
        QuickSearchTextBox.Clear();
        _dataTable.DefaultView.RowFilter = string.Empty;
        UpdateResultInfo();
        FilterApplied?.Invoke(string.Empty);
    }
    
    /// <summary>
    /// 닫기 버튼
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

/// <summary>
/// 필터 조건
/// </summary>
public class FilterCondition : System.ComponentModel.INotifyPropertyChanged
{
    private string _logicalOperator = "";
    private bool _showLogicalOperator;
    
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "=";
    public string Value { get; set; } = string.Empty;
    
    public string LogicalOperator
    {
        get => _logicalOperator;
        set
        {
            _logicalOperator = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(LogicalOperator)));
        }
    }
    
    public bool ShowLogicalOperator
    {
        get => _showLogicalOperator;
        set
        {
            _showLogicalOperator = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ShowLogicalOperator)));
        }
    }
    
    public string DisplayText => $"{Field} {GetOperatorDisplay()} {(Operator == "NULL" || Operator == "NOTNULL" ? "" : $"'{Value}'")}";
    
    private string GetOperatorDisplay()
    {
        return Operator switch
        {
            "=" => "=",
            "<>" => "≠",
            ">" => ">",
            ">=" => "≥",
            "<" => "<",
            "<=" => "≤",
            "LIKE" => "포함",
            "STARTS" => "시작",
            "ENDS" => "끝",
            "NULL" => "IS NULL",
            "NOTNULL" => "IS NOT NULL",
            _ => Operator
        };
    }
    
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
