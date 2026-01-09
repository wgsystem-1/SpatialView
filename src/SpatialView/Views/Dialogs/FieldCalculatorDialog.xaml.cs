using System.Data;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace SpatialView.Views.Dialogs;

/// <summary>
/// 필드 계산기 다이얼로그
/// </summary>
public partial class FieldCalculatorDialog : Window
{
    private readonly DataTable _dataTable;
    public string? TargetField { get; private set; }
    public string Expression => ExpressionTextBox.Text.Trim();
    
    // 새 필드 생성 정보
    public bool IsNewField { get; private set; }
    public string? NewFieldName { get; private set; }
    public Type? NewFieldType { get; private set; }
    
    public FieldCalculatorDialog(DataTable dataTable)
    {
        InitializeComponent();
        _dataTable = dataTable;
        
        // 필드 목록 채우기
        foreach (DataColumn col in dataTable.Columns)
        {
            if (col.ColumnName != "FID")
            {
                TargetFieldComboBox.Items.Add(col.ColumnName);
                FieldsListBox.Items.Add($"[{col.ColumnName}]");
            }
        }
        
        if (TargetFieldComboBox.Items.Count > 0)
            TargetFieldComboBox.SelectedIndex = 0;
    }
    
    private void FieldsListBox_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (FieldsListBox.SelectedItem is string field)
        {
            InsertAtCursor(field);
        }
    }
    
    private void FunctionsListBox_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (FunctionsListBox.SelectedItem is ListBoxItem item && item.Content is string func)
        {
            InsertAtCursor(func);
        }
    }
    
    private void InsertAtCursor(string text)
    {
        var caretIndex = ExpressionTextBox.CaretIndex;
        ExpressionTextBox.Text = ExpressionTextBox.Text.Insert(caretIndex, text);
        ExpressionTextBox.CaretIndex = caretIndex + text.Length;
        ExpressionTextBox.Focus();
    }
    
    private void NewField_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddFieldDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            IsNewField = true;
            NewFieldName = dialog.FieldName;
            NewFieldType = dialog.FieldType;
            
            // 콤보박스에 추가하고 선택
            TargetFieldComboBox.Items.Add(dialog.FieldName);
            TargetFieldComboBox.SelectedItem = dialog.FieldName;
        }
    }
    
    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Expression))
        {
            MessageBox.Show("계산식을 입력하세요.", "알림", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        try
        {
            // 첫 번째 행으로 미리보기
            if (_dataTable.Rows.Count > 0)
            {
                var result = EvaluateExpression(_dataTable.Rows[0], Expression);
                MessageBox.Show($"첫 번째 행의 결과: {result}", "미리보기", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("데이터가 없습니다.", "알림", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"계산식 오류: {ex.Message}", "오류", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (TargetFieldComboBox.SelectedItem == null)
        {
            MessageBox.Show("대상 필드를 선택하세요.", "오류", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (string.IsNullOrWhiteSpace(Expression))
        {
            MessageBox.Show("계산식을 입력하세요.", "오류", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        TargetField = TargetFieldComboBox.SelectedItem.ToString();
        DialogResult = true;
        Close();
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    
    /// <summary>
    /// 표현식 평가
    /// </summary>
    public static object? EvaluateExpression(DataRow row, string expression)
    {
        var result = expression;
        
        // [필드명] 패턴을 실제 값으로 치환
        var fieldPattern = new Regex(@"\[(\w+)\]");
        var matches = fieldPattern.Matches(expression);
        
        foreach (Match match in matches)
        {
            var fieldName = match.Groups[1].Value;
            if (row.Table.Columns.Contains(fieldName))
            {
                var value = row[fieldName];
                var valueStr = value?.ToString() ?? "";
                
                // 숫자인 경우 그대로, 문자열인 경우 따옴표 추가
                if (value is int || value is long || value is float || value is double || value is decimal)
                {
                    result = result.Replace(match.Value, valueStr);
                }
                else
                {
                    result = result.Replace(match.Value, $"\"{valueStr}\"");
                }
            }
        }
        
        // 간단한 함수 처리
        result = ProcessFunctions(result, row);
        
        // DataTable.Compute를 이용한 계산 시도
        try
        {
            // 순수 숫자 연산인 경우
            if (Regex.IsMatch(result, @"^[\d\s\+\-\*\/\.\(\)]+$"))
            {
                var tempTable = new DataTable();
                var computed = tempTable.Compute(result, null);
                return computed;
            }
            else
            {
                // 문자열 결과 반환
                return result.Trim('"');
            }
        }
        catch
        {
            return result.Trim('"');
        }
    }
    
    private static string ProcessFunctions(string expression, DataRow row)
    {
        var result = expression;
        
        // LENGTH 함수
        result = Regex.Replace(result, @"LENGTH\(""([^""]*)""\)", m => 
            m.Groups[1].Value.Length.ToString(), RegexOptions.IgnoreCase);
        
        // UPPER 함수
        result = Regex.Replace(result, @"UPPER\(""([^""]*)""\)", m => 
            $"\"{m.Groups[1].Value.ToUpper()}\"", RegexOptions.IgnoreCase);
        
        // LOWER 함수
        result = Regex.Replace(result, @"LOWER\(""([^""]*)""\)", m => 
            $"\"{m.Groups[1].Value.ToLower()}\"", RegexOptions.IgnoreCase);
        
        // TRIM 함수
        result = Regex.Replace(result, @"TRIM\(""([^""]*)""\)", m => 
            $"\"{m.Groups[1].Value.Trim()}\"", RegexOptions.IgnoreCase);
        
        // ABS 함수
        result = Regex.Replace(result, @"ABS\(([\d\.\-]+)\)", m =>
        {
            if (double.TryParse(m.Groups[1].Value, out var val))
                return Math.Abs(val).ToString();
            return m.Value;
        }, RegexOptions.IgnoreCase);
        
        // ROUND 함수
        result = Regex.Replace(result, @"ROUND\(([\d\.\-]+)\s*,\s*(\d+)\)", m =>
        {
            if (double.TryParse(m.Groups[1].Value, out var val) && 
                int.TryParse(m.Groups[2].Value, out var digits))
                return Math.Round(val, digits).ToString();
            return m.Value;
        }, RegexOptions.IgnoreCase);
        
        // CONCAT 함수
        result = Regex.Replace(result, @"CONCAT\(""([^""]*)""\s*,\s*""([^""]*)""\)", m => 
            $"\"{m.Groups[1].Value}{m.Groups[2].Value}\"", RegexOptions.IgnoreCase);
        
        return result;
    }
}

