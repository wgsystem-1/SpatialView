using System.Windows;

namespace SpatialView.Views;

/// <summary>
/// 피처 속성 팝업 창
/// </summary>
public partial class FeaturePopupWindow : Window
{
    // 마지막 창 위치 저장 (static으로 유지)
    private static double? _lastLeft = null;
    private static double? _lastTop = null;
    private static double? _lastWidth = null;
    private static double? _lastHeight = null;
    
    public FeaturePopupWindow()
    {
        InitializeComponent();
        
        // 이전 위치가 있으면 복원
        if (_lastLeft.HasValue && _lastTop.HasValue)
        {
            Left = _lastLeft.Value;
            Top = _lastTop.Value;
        }
        else
        {
            // 처음 열 때는 화면 오른쪽에 배치
            var screen = SystemParameters.WorkArea;
            Left = screen.Right - Width - 20;
            Top = screen.Top + 100;
        }
        
        // 크기 복원
        if (_lastWidth.HasValue && _lastHeight.HasValue)
        {
            Width = _lastWidth.Value;
            Height = _lastHeight.Value;
        }
        
        // 위치/크기 변경 시 저장
        LocationChanged += (s, e) => SavePosition();
        SizeChanged += (s, e) => SaveSize();
    }
    
    public FeaturePopupWindow(FeaturePopupViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
    
    private void SavePosition()
    {
        _lastLeft = Left;
        _lastTop = Top;
    }
    
    private void SaveSize()
    {
        _lastWidth = Width;
        _lastHeight = Height;
    }
    
    /// <summary>
    /// ViewModel 업데이트 (새 창을 만들지 않고 내용만 갱신)
    /// </summary>
    public void UpdateViewModel(FeaturePopupViewModel viewModel)
    {
        DataContext = viewModel;
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

