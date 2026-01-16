using System.Windows;
using System.Windows.Controls;
using SpatialView.ViewModels;

// 명시적 타입 지정
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;

namespace SpatialView.Views.Dialogs;

/// <summary>
/// 레이어 스타일 설정 다이얼로그
/// </summary>
public partial class LayerStyleDialog : Window
{
    private readonly LayerItemViewModel _layerItem;
    
    // 스타일 값들
    private WpfColor _fillColor = WpfColors.LightBlue;
    private WpfColor _lineColor = WpfColors.DarkBlue;
    private WpfColor _symbolColor = WpfColors.Red;
    private WpfColor _symbolOutlineColor = WpfColors.Black;
    
    public LayerStyleDialog(LayerItemViewModel layerItem)
    {
        InitializeComponent();
        _layerItem = layerItem;
        
        LayerNameText.Text = layerItem.Name;
        
        LoadCurrentStyle();
    }
    
    /// <summary>
    /// 현재 스타일 로드
    /// </summary>
    private void LoadCurrentStyle()
    {
        // 레이어 색상에서 초기값 설정 (System.Drawing.Color -> WpfColor 변환)
        var drawingColor = _layerItem.LayerColor;
        _fillColor = WpfColor.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);
        _lineColor = _fillColor;
        _symbolColor = _fillColor;
        
        // 미리보기 업데이트
        UpdateColorPreviews();
        
        // 투명도 설정
        FillOpacitySlider.Value = _layerItem.Opacity * 100;
        
        // 스타일 속성이 있으면 로드 (IVectorLayer로 캐스팅)
        if (_layerItem.Layer is Core.GisEngine.IVectorLayer vectorLayer && vectorLayer.Style != null)
        {
            var style = vectorLayer.Style;
            
            // 채움 색상
            if (style.FillColor != null)
            {
                _fillColor = WpfColor.FromArgb(
                    style.FillColor.Value.A,
                    style.FillColor.Value.R,
                    style.FillColor.Value.G,
                    style.FillColor.Value.B);
            }
            
            // 선 색상
            if (style.LineColor != null)
            {
                _lineColor = WpfColor.FromArgb(
                    style.LineColor.Value.A,
                    style.LineColor.Value.R,
                    style.LineColor.Value.G,
                    style.LineColor.Value.B);
            }
            
            // 선 두께
            LineWidthSlider.Value = style.LineWidth;
            
            // 심볼 크기
            SymbolSizeSlider.Value = style.SymbolSize;
            
            UpdateColorPreviews();
        }
    }
    
    /// <summary>
    /// 색상 미리보기 업데이트
    /// </summary>
    private void UpdateColorPreviews()
    {
        FillColorPreview.Background = new System.Windows.Media.SolidColorBrush(_fillColor);
        LineColorPreview.Background = new System.Windows.Media.SolidColorBrush(_lineColor);
        SymbolColorPreview.Background = new System.Windows.Media.SolidColorBrush(_symbolColor);
        SymbolOutlineColorPreview.Background = new System.Windows.Media.SolidColorBrush(_symbolOutlineColor);
    }
    
    #region 채움 스타일 이벤트
    
    private void SelectFillColor_Click(object sender, RoutedEventArgs e)
    {
        var colorDialog = new System.Windows.Forms.ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(_fillColor.A, _fillColor.R, _fillColor.G, _fillColor.B)
        };
        
        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _fillColor = WpfColor.FromArgb(
                colorDialog.Color.A,
                colorDialog.Color.R,
                colorDialog.Color.G,
                colorDialog.Color.B);
            UpdateColorPreviews();
        }
    }
    
    private void FillOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FillOpacityText != null)
        {
            FillOpacityText.Text = $"{(int)e.NewValue}%";
        }
    }
    
    private void FillPattern_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 패턴 변경 처리
    }
    
    #endregion
    
    #region 선 스타일 이벤트
    
    private void SelectLineColor_Click(object sender, RoutedEventArgs e)
    {
        var colorDialog = new System.Windows.Forms.ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(_lineColor.A, _lineColor.R, _lineColor.G, _lineColor.B)
        };
        
        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _lineColor = WpfColor.FromArgb(
                colorDialog.Color.A,
                colorDialog.Color.R,
                colorDialog.Color.G,
                colorDialog.Color.B);
            UpdateColorPreviews();
        }
    }
    
    private void LineWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LineWidthText != null)
        {
            LineWidthText.Text = $"{e.NewValue:F1} px";
        }
    }
    
    private void LineStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 선 스타일 변경 처리
    }
    
    #endregion
    
    #region 심볼 스타일 이벤트
    
    private void SelectSymbolColor_Click(object sender, RoutedEventArgs e)
    {
        var colorDialog = new System.Windows.Forms.ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(_symbolColor.A, _symbolColor.R, _symbolColor.G, _symbolColor.B)
        };
        
        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _symbolColor = WpfColor.FromArgb(
                colorDialog.Color.A,
                colorDialog.Color.R,
                colorDialog.Color.G,
                colorDialog.Color.B);
            UpdateColorPreviews();
        }
    }
    
    private void SelectSymbolOutlineColor_Click(object sender, RoutedEventArgs e)
    {
        var colorDialog = new System.Windows.Forms.ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(_symbolOutlineColor.A, _symbolOutlineColor.R, _symbolOutlineColor.G, _symbolOutlineColor.B)
        };
        
        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _symbolOutlineColor = WpfColor.FromArgb(
                colorDialog.Color.A,
                colorDialog.Color.R,
                colorDialog.Color.G,
                colorDialog.Color.B);
            UpdateColorPreviews();
        }
    }
    
    private void SymbolType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 심볼 타입 변경 처리
    }
    
    private void SymbolSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SymbolSizeText != null)
        {
            SymbolSizeText.Text = $"{(int)e.NewValue} px";
        }
    }
    
    private void SymbolOutlineWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SymbolOutlineWidthText != null)
        {
            SymbolOutlineWidthText.Text = $"{e.NewValue:F1} px";
        }
    }
    
    #endregion
    
    #region 버튼 이벤트
    
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        ApplyStyle();
    }
    
    private void OK_Click(object sender, RoutedEventArgs e)
    {
        ApplyStyle();
        DialogResult = true;
        Close();
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    
    /// <summary>
    /// 스타일 적용
    /// </summary>
    private void ApplyStyle()
    {
        if (_layerItem.Layer is not Core.GisEngine.IVectorLayer vectorLayer || vectorLayer.Style == null) return;
        
        var style = vectorLayer.Style;
        
        // WPF Color를 Media Color로 변환하여 스타일에 적용
        var fillMediaColor = System.Windows.Media.Color.FromArgb(_fillColor.A, _fillColor.R, _fillColor.G, _fillColor.B);
        var lineMediaColor = System.Windows.Media.Color.FromArgb(_lineColor.A, _lineColor.R, _lineColor.G, _lineColor.B);
        
        // 채움 색상 적용 (IVectorStyle.Fill은 Media.Color)
        style.Fill = fillMediaColor;
        
        // 외곽선 색상 적용 (IVectorStyle.Outline은 Media.Color)
        style.Outline = lineMediaColor;
        
        // 채움 색상 적용 (Drawing.Color)
        style.FillColor = System.Drawing.Color.FromArgb(
            _fillColor.A, _fillColor.R, _fillColor.G, _fillColor.B);
        
        // 선 색상 적용 (Drawing.Color)
        style.LineColor = System.Drawing.Color.FromArgb(
            _lineColor.A, _lineColor.R, _lineColor.G, _lineColor.B);
        
        // 선 두께 적용
        style.LineWidth = (float)LineWidthSlider.Value;
        style.OutlineWidth = (float)LineWidthSlider.Value;
        
        // 심볼 색상 적용
        style.PointColor = System.Drawing.Color.FromArgb(
            _symbolColor.A, _symbolColor.R, _symbolColor.G, _symbolColor.B);
        
        // 심볼 크기 적용
        style.SymbolSize = (float)SymbolSizeSlider.Value;
        style.PointSize = (float)SymbolSizeSlider.Value;
        
        // 투명도 적용
        style.Opacity = (float)(FillOpacitySlider.Value / 100.0);
        _layerItem.Opacity = FillOpacitySlider.Value / 100.0;
        
        // 심볼 타입 적용
        if (SymbolTypeComboBox.SelectedIndex >= 0)
        {
            style.SymbolType = (Core.Styling.SymbolType)SymbolTypeComboBox.SelectedIndex;
        }
        
        // 선 스타일 적용
        if (LineStyleComboBox.SelectedIndex >= 0)
        {
            style.LineStyle = (Core.Styling.LineStyle)LineStyleComboBox.SelectedIndex;
        }
        
        // 심볼 변환 옵션 적용
        if (ConvertToSymbolCheckBox.IsChecked == true)
        {
            style.RenderAsSymbol = true;
            if (ConvertSymbolTypeComboBox.SelectedIndex >= 0)
            {
                style.SymbolType = (Core.Styling.SymbolType)ConvertSymbolTypeComboBox.SelectedIndex;
            }
            style.SymbolSize = (float)ConvertSymbolSizeSlider.Value;
        }
        else
        {
            style.RenderAsSymbol = false;
        }
        
        // 레이어 색상 업데이트 (UI 아이콘용) - 지오메트리 타입별로 대표 색상 선택
        System.Drawing.Color uiColor;
        if (_layerItem.GeometryType == Core.Enums.GeometryType.Point || _layerItem.GeometryType == Core.Enums.GeometryType.MultiPoint || style.RenderAsSymbol)
        {
            // 점/심볼 → 심볼 색상 사용
            uiColor = System.Drawing.Color.FromArgb(_symbolColor.A, _symbolColor.R, _symbolColor.G, _symbolColor.B);
        }
        else if (_layerItem.GeometryType == Core.Enums.GeometryType.Line || _layerItem.GeometryType == Core.Enums.GeometryType.LineString || _layerItem.GeometryType == Core.Enums.GeometryType.MultiLineString)
        {
            // 라인 → 선 색상 사용
            uiColor = System.Drawing.Color.FromArgb(_lineColor.A, _lineColor.R, _lineColor.G, _lineColor.B);
        }
        else
        {
            // 폴리곤 등 → 채움 색상 사용
            uiColor = System.Drawing.Color.FromArgb(_fillColor.A, _fillColor.R, _fillColor.G, _fillColor.B);
        }
        _layerItem.LayerColor = uiColor;
        
        // SpatialViewVectorLayerAdapter의 경우 스타일을 엔진에 동기화
        if (_layerItem.Layer is Infrastructure.GisEngine.SpatialViewVectorLayerAdapter adapter)
        {
            // Style 속성을 다시 설정하여 SyncStyleToEngine() 호출
            adapter.Style = style;
        }
        
        // 지도 새로고침 요청 (레이어 변경 이벤트 + Refresh)
        _layerItem.RaiseLayerChanged();
        _layerItem.Layer?.Refresh();
    }
    
    #endregion
}
