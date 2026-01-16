using System.Linq;
using System.Windows;
using System.Windows.Media;
using SpatialView.ViewModels;

namespace SpatialView.Views.Dialogs;

/// <summary>
/// 라벨 설정 다이얼로그
/// </summary>
public partial class LabelSettingsDialog : Window
{
    private readonly LayerItemViewModel _layerItem;
    
    public LabelSettingsDialog(LayerItemViewModel layerItem)
    {
        InitializeComponent();
        
        _layerItem = layerItem ?? throw new ArgumentNullException(nameof(layerItem));
        
        // 속성 필드 목록 로드
        _layerItem.LoadAvailableFields();
        
        // 시스템 폰트 목록 로드 (한글 폰트 우선)
        var fonts = Fonts.SystemFontFamilies
            .OrderByDescending(f => IsKoreanFont(f))
            .ThenBy(f => f.Source)
            .ToList();
        FontFamilyComboBox.ItemsSource = fonts;
        
        // 현재 선택된 폰트가 없으면 기본값 설정
        if (_layerItem.LabelFontFamily == null)
        {
            _layerItem.LabelFontFamily = fonts.FirstOrDefault(f => f.Source.Contains("Malgun")) 
                                         ?? fonts.FirstOrDefault() 
                                         ?? new System.Windows.Media.FontFamily("Malgun Gothic");
        }
        
        DataContext = _layerItem;
    }
    
    /// <summary>
    /// 한글 폰트인지 확인
    /// </summary>
    private static bool IsKoreanFont(System.Windows.Media.FontFamily font)
    {
        var koreanFonts = new[] { "Malgun", "맑은", "굴림", "Gulim", "돋움", "Dotum", "바탕", "Batang", "궁서", "Gungsuh", "나눔", "Nanum" };
        return koreanFonts.Any(k => font.Source.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
    
    private void FontColorButton_Click(object sender, RoutedEventArgs e)
    {
        var colorDialog = new System.Windows.Forms.ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(
                _layerItem.LabelColor.A,
                _layerItem.LabelColor.R,
                _layerItem.LabelColor.G,
                _layerItem.LabelColor.B)
        };
        
        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _layerItem.LabelColor = System.Windows.Media.Color.FromArgb(
                colorDialog.Color.A,
                colorDialog.Color.R,
                colorDialog.Color.G,
                colorDialog.Color.B);
        }
    }
    
    private void HaloColorButton_Click(object sender, RoutedEventArgs e)
    {
        var colorDialog = new System.Windows.Forms.ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(
                _layerItem.LabelHaloColor.A,
                _layerItem.LabelHaloColor.R,
                _layerItem.LabelHaloColor.G,
                _layerItem.LabelHaloColor.B)
        };
        
        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _layerItem.LabelHaloColor = System.Windows.Media.Color.FromArgb(
                colorDialog.Color.A,
                colorDialog.Color.R,
                colorDialog.Color.G,
                colorDialog.Color.B);
        }
    }
    
    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
