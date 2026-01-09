using System.Diagnostics;
using System.Windows;
using Clipboard = System.Windows.Clipboard;

namespace SpatialView.Views.Dialogs;

/// <summary>
/// 오류 다이얼로그
/// </summary>
public partial class ErrorDialog : Window
{
    private readonly string _logDirectory;
    private readonly string _fullErrorText;
    
    public ErrorDialog(string title, string message, string? details = null, string? logDirectory = null)
    {
        InitializeComponent();
        
        TitleTextBlock.Text = title;
        ErrorMessageTextBlock.Text = message;
        
        _logDirectory = logDirectory ?? GetDefaultLogDirectory();
        
        if (!string.IsNullOrEmpty(details))
        {
            DetailsTextBlock.Text = details;
            DetailsExpander.Visibility = Visibility.Visible;
        }
        else
        {
            DetailsExpander.Visibility = Visibility.Collapsed;
        }
        
        _fullErrorText = $"[{title}]\n{message}";
        if (!string.IsNullOrEmpty(details))
        {
            _fullErrorText += $"\n\n[상세 정보]\n{details}";
        }
    }
    
    private static string GetDefaultLogDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return System.IO.Path.Combine(localAppData, "SpatialView", "Logs");
    }
    
    private void OK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
    
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_fullErrorText);
            System.Windows.MessageBox.Show("오류 정보가 클립보드에 복사되었습니다.", "복사 완료",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"클립보드 복사 오류: {ex.Message}");
        }
    }
    
    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (System.IO.Directory.Exists(_logDirectory))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _logDirectory,
                    UseShellExecute = true
                });
            }
            else
            {
                System.Windows.MessageBox.Show("로그 폴더가 존재하지 않습니다.", "알림",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"폴더 열기 오류: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 간단한 오류 다이얼로그 표시
    /// </summary>
    public static void Show(string message, string? details = null, Window? owner = null)
    {
        var dialog = new ErrorDialog("오류가 발생했습니다", message, details)
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }
    
    /// <summary>
    /// Exception에서 오류 다이얼로그 표시
    /// </summary>
    public static void ShowException(Exception exception, string? userMessage = null, Window? owner = null)
    {
        var message = userMessage ?? exception.Message;
        var details = $"예외 유형: {exception.GetType().FullName}\n\n{exception.StackTrace}";
        
        if (exception.InnerException != null)
        {
            details += $"\n\n내부 예외: {exception.InnerException.Message}";
        }
        
        var dialog = new ErrorDialog("오류가 발생했습니다", message, details)
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }
}

