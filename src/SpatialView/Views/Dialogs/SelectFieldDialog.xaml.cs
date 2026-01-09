using System.Collections.Generic;
using System.Windows;

namespace SpatialView.Views.Dialogs;

/// <summary>
/// 필드 선택 다이얼로그
/// </summary>
public partial class SelectFieldDialog : Window
{
    public string? SelectedField { get; private set; }
    
    public SelectFieldDialog(IEnumerable<string> fields, string title = "필드 선택")
    {
        InitializeComponent();
        
        TitleTextBlock.Text = title;
        Title = title;
        
        foreach (var field in fields)
        {
            FieldListBox.Items.Add(field);
        }
        
        if (FieldListBox.Items.Count > 0)
        {
            FieldListBox.SelectedIndex = 0;
        }
    }
    
    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (FieldListBox.SelectedItem == null)
        {
            System.Windows.MessageBox.Show("필드를 선택하세요.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        SelectedField = FieldListBox.SelectedItem.ToString();
        DialogResult = true;
        Close();
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

