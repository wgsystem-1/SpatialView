using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace SpatialView.Views.Dialogs;

/// <summary>
/// 필드 추가 다이얼로그
/// </summary>
public partial class AddFieldDialog : Window
{
    public string FieldName => FieldNameTextBox.Text.Trim();
    
    public Type FieldType => FieldTypeComboBox.SelectedIndex switch
    {
        0 => typeof(string),
        1 => typeof(int),
        2 => typeof(double),
        3 => typeof(DateTime),
        _ => typeof(string)
    };
    
    public string DefaultValue => DefaultValueTextBox.Text.Trim();
    
    public AddFieldDialog()
    {
        InitializeComponent();
        FieldNameTextBox.Focus();
    }
    
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FieldName))
        {
            MessageBox.Show("필드 이름을 입력하세요.", "오류", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            FieldNameTextBox.Focus();
            return;
        }
        
        // 필드 이름 유효성 검사 (영문, 숫자, 언더스코어만)
        if (!System.Text.RegularExpressions.Regex.IsMatch(FieldName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
        {
            MessageBox.Show("필드 이름은 영문자로 시작하고, 영문/숫자/언더스코어만 사용할 수 있습니다.", 
                "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            FieldNameTextBox.Focus();
            return;
        }
        
        DialogResult = true;
        Close();
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

