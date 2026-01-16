using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SpatialView.Views.Dialogs;

/// <summary>
/// 내보내기 다이얼로그
/// </summary>
public partial class ExportDialog : Window
{
    private readonly Canvas _mapCanvas;
    private readonly double _currentWidth;
    private readonly double _currentHeight;
    
    /// <summary>
    /// 내보내기 형식
    /// </summary>
    public enum ExportFormat
    {
        Png,
        Jpeg,
        Pdf
    }
    
    /// <summary>
    /// 내보내기 결과
    /// </summary>
    public class ExportResult
    {
        public bool Success { get; set; }
        public string? FilePath { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public ExportDialog(Canvas mapCanvas)
    {
        InitializeComponent();
        
        _mapCanvas = mapCanvas ?? throw new ArgumentNullException(nameof(mapCanvas));
        _currentWidth = mapCanvas.ActualWidth;
        _currentHeight = mapCanvas.ActualHeight;
        
        // 현재 화면 크기로 초기화
        WidthTextBox.Text = ((int)_currentWidth).ToString();
        HeightTextBox.Text = ((int)_currentHeight).ToString();
        
        // 이벤트 연결
        WidthTextBox.TextChanged += Size_TextChanged;
        HeightTextBox.TextChanged += Size_TextChanged;
        PngRadio.Checked += Format_Changed;
        JpegRadio.Checked += Format_Changed;
        PdfRadio.Checked += Format_Changed;
        
        UpdatePreviewInfo();
        
        // 로드 완료 후 미리보기 생성
        Loaded += (s, e) => GeneratePreviewAsync();
    }
    
    /// <summary>
    /// 크기 변경 시 미리보기 업데이트
    /// </summary>
    private void Size_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (MaintainAspectRatioCheckBox?.IsChecked == true && sender is System.Windows.Controls.TextBox textBox)
        {
            double aspectRatio = _currentWidth / _currentHeight;
            
            if (textBox == WidthTextBox && int.TryParse(WidthTextBox.Text, out int width))
            {
                HeightTextBox.TextChanged -= Size_TextChanged;
                HeightTextBox.Text = ((int)(width / aspectRatio)).ToString();
                HeightTextBox.TextChanged += Size_TextChanged;
            }
            else if (textBox == HeightTextBox && int.TryParse(HeightTextBox.Text, out int height))
            {
                WidthTextBox.TextChanged -= Size_TextChanged;
                WidthTextBox.Text = ((int)(height * aspectRatio)).ToString();
                WidthTextBox.TextChanged += Size_TextChanged;
            }
        }
        
        UpdatePreviewInfo();
    }
    
    /// <summary>
    /// 형식 변경 시 미리보기 업데이트
    /// </summary>
    private void Format_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreviewInfo();
    }
    
    /// <summary>
    /// 옵션 변경 시 미리보기 업데이트
    /// </summary>
    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        GeneratePreviewAsync();
    }
    
    /// <summary>
    /// 미리보기 새로고침 버튼
    /// </summary>
    private void RefreshPreview_Click(object sender, RoutedEventArgs e)
    {
        GeneratePreviewAsync();
    }
    
    /// <summary>
    /// 미리보기 정보 업데이트
    /// </summary>
    private void UpdatePreviewInfo()
    {
        if (PreviewInfoText == null) return;
        
        string format = "PNG";
        if (JpegRadio?.IsChecked == true) format = "JPEG";
        else if (PdfRadio?.IsChecked == true) format = "PDF";
        
        string width = WidthTextBox?.Text ?? "1920";
        string height = HeightTextBox?.Text ?? "1080";
        
        PreviewInfoText.Text = $"{width} x {height} px, {format}";
    }
    
    /// <summary>
    /// 미리보기 이미지 비동기 생성
    /// </summary>
    private async void GeneratePreviewAsync()
    {
        if (LoadingPanel == null || PreviewImage == null || PreviewStatusText == null) return;
        
        try
        {
            LoadingPanel.Visibility = Visibility.Visible;
            PreviewStatusText.Text = "미리보기 생성 중...";
            
            // UI 스레드에서 캔버스 렌더링
            await Task.Delay(50); // UI 업데이트를 위한 짧은 대기
            
            // 미리보기용 작은 크기로 렌더링 (성능을 위해)
            int previewWidth = 400;
            int previewHeight = (int)(previewWidth * _currentHeight / _currentWidth);
            
            var previewBitmap = RenderPreviewBitmap(previewWidth, previewHeight);
            
            PreviewImage.Source = previewBitmap;
            PreviewStatusText.Text = $"미리보기 생성 완료 ({DateTime.Now:HH:mm:ss})";
        }
        catch (Exception ex)
        {
            PreviewStatusText.Text = $"미리보기 오류: {ex.Message}";
        }
        finally
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }
    
    /// <summary>
    /// 미리보기용 비트맵 렌더링
    /// </summary>
    private RenderTargetBitmap RenderPreviewBitmap(int width, int height)
    {
        // 캔버스 크기 확인
        double canvasWidth = _mapCanvas.ActualWidth;
        double canvasHeight = _mapCanvas.ActualHeight;
        
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            canvasWidth = _currentWidth > 0 ? _currentWidth : 800;
            canvasHeight = _currentHeight > 0 ? _currentHeight : 600;
        }
        
        // 렌더 타겟 비트맵 생성 (원본 캔버스 크기로)
        var canvasBitmap = new RenderTargetBitmap(
            (int)canvasWidth, (int)canvasHeight, 96, 96, PixelFormats.Pbgra32);
        
        // 캔버스 직접 렌더링
        canvasBitmap.Render(_mapCanvas);
        
        // 최종 출력 비트맵 생성
        var renderBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        
        // 드로잉 비주얼 생성
        var drawingVisual = new DrawingVisual();
        using (var dc = drawingVisual.RenderOpen())
        {
            // 배경 그리기
            dc.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, width, height));
            
            // 캔버스 비트맵 그리기
            dc.DrawImage(canvasBitmap, new Rect(0, 0, width, height));
            
            // 축척 막대 그리기 (옵션)
            if (IncludeScaleBarCheckBox?.IsChecked == true)
            {
                DrawScaleBar(dc, width, height);
            }
            
            // 방위표 그리기 (옵션)
            if (IncludeNorthArrowCheckBox?.IsChecked == true)
            {
                DrawNorthArrow(dc, width, height);
            }
            
            // 범례 그리기 (옵션)
            if (IncludeLegendCheckBox?.IsChecked == true)
            {
                DrawLegend(dc, width, height);
            }
            
            // 제목 그리기 (옵션)
            if (IncludeTitleCheckBox?.IsChecked == true)
            {
                DrawTitle(dc, width, height);
            }
        }
        
        renderBitmap.Render(drawingVisual);
        
        return renderBitmap;
    }
    
    /// <summary>
    /// 내보내기 버튼 클릭
    /// </summary>
    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 크기 파싱
            if (!int.TryParse(WidthTextBox.Text, out int width) || width <= 0)
            {
                System.Windows.MessageBox.Show("유효한 너비를 입력하세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (!int.TryParse(HeightTextBox.Text, out int height) || height <= 0)
            {
                System.Windows.MessageBox.Show("유효한 높이를 입력하세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // DPI 파싱
            int dpi = 150;
            if (DpiComboBox.SelectedItem is ComboBoxItem dpiItem && dpiItem.Tag is string dpiStr)
            {
                int.TryParse(dpiStr, out dpi);
            }
            
            // 형식 결정
            ExportFormat format = ExportFormat.Png;
            string filter = "PNG 이미지|*.png";
            string defaultExt = ".png";
            
            if (JpegRadio.IsChecked == true)
            {
                format = ExportFormat.Jpeg;
                filter = "JPEG 이미지|*.jpg;*.jpeg";
                defaultExt = ".jpg";
            }
            else if (PdfRadio.IsChecked == true)
            {
                format = ExportFormat.Pdf;
                filter = "PDF 문서|*.pdf";
                defaultExt = ".pdf";
            }
            
            // 파일 저장 다이얼로그
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = filter,
                DefaultExt = defaultExt,
                FileName = $"Map_Export_{DateTime.Now:yyyyMMdd_HHmmss}"
            };
            
            if (saveDialog.ShowDialog() != true)
                return;
            
            // 내보내기 실행
            var result = ExportMap(saveDialog.FileName, width, height, dpi, format);
            
            if (result.Success)
            {
                System.Windows.MessageBox.Show($"내보내기 완료!\n{result.FilePath}", "완료", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show($"내보내기 실패: {result.ErrorMessage}", "오류", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"내보내기 오류: {ex.Message}", "오류", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    /// <summary>
    /// 지도 내보내기 실행
    /// </summary>
    private ExportResult ExportMap(string filePath, int width, int height, int dpi, ExportFormat format)
    {
        try
        {
            // 캔버스를 비트맵으로 렌더링
            var renderBitmap = RenderCanvasToBitmap(width, height, dpi);
            
            if (format == ExportFormat.Pdf)
            {
                // PDF 내보내기
                return ExportToPdf(filePath, renderBitmap, width, height, dpi);
            }
            else
            {
                // 이미지 내보내기
                return ExportToImage(filePath, renderBitmap, format);
            }
        }
        catch (Exception ex)
        {
            return new ExportResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
    
    /// <summary>
    /// 캔버스를 비트맵으로 렌더링
    /// </summary>
    private RenderTargetBitmap RenderCanvasToBitmap(int width, int height, int dpi)
    {
        // 캔버스 크기 확인
        double canvasWidth = _mapCanvas.ActualWidth;
        double canvasHeight = _mapCanvas.ActualHeight;
        
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            canvasWidth = _currentWidth > 0 ? _currentWidth : 800;
            canvasHeight = _currentHeight > 0 ? _currentHeight : 600;
        }
        
        // 스케일 계산
        double scaleX = (double)width / canvasWidth;
        double scaleY = (double)height / canvasHeight;
        
        // 렌더 타겟 비트맵 생성 (원본 캔버스 크기로)
        var canvasBitmap = new RenderTargetBitmap(
            (int)canvasWidth, (int)canvasHeight, 96, 96, PixelFormats.Pbgra32);
        
        // 캔버스 직접 렌더링
        canvasBitmap.Render(_mapCanvas);
        
        // 최종 출력 비트맵 생성 (96 DPI로 고정하여 좌표 일관성 유지)
        var renderBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        
        // 드로잉 비주얼 생성
        var drawingVisual = new DrawingVisual();
        using (var dc = drawingVisual.RenderOpen())
        {
            // 배경 그리기
            dc.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, width, height));
            
            // 캔버스 비트맵 그리기 (스케일 적용)
            dc.DrawImage(canvasBitmap, new Rect(0, 0, width, height));
            
            // 축척 막대 그리기 (옵션)
            if (IncludeScaleBarCheckBox.IsChecked == true)
            {
                DrawScaleBar(dc, width, height);
            }
            
            // 방위표 그리기 (옵션)
            if (IncludeNorthArrowCheckBox.IsChecked == true)
            {
                DrawNorthArrow(dc, width, height);
            }
            
            // 범례 그리기 (옵션)
            if (IncludeLegendCheckBox.IsChecked == true)
            {
                DrawLegend(dc, width, height);
            }
            
            // 제목 그리기 (옵션)
            if (IncludeTitleCheckBox.IsChecked == true)
            {
                DrawTitle(dc, width, height);
            }
        }
        
        renderBitmap.Render(drawingVisual);
        
        return renderBitmap;
    }
    
    /// <summary>
    /// 축척 막대 그리기
    /// </summary>
    private void DrawScaleBar(DrawingContext dc, int width, int height)
    {
        var barWidth = width * 0.2; // 화면 너비의 20%
        var barHeight = 10;
        var x = 20.0;
        var y = height - 50.0;
        var padding = 10.0;
        
        // 배경 박스
        var bgRect = new Rect(x - padding, y - 25, barWidth + padding * 2, barHeight + 35);
        dc.DrawRectangle(
            new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 255, 255, 255)), 
            new System.Windows.Media.Pen(System.Windows.Media.Brushes.Gray, 1), 
            bgRect);
        
        // 축척 막대 테두리
        var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.Black, 1);
        dc.DrawRectangle(System.Windows.Media.Brushes.White, pen, new Rect(x, y, barWidth, barHeight));
        
        // 흑백 패턴
        var segmentWidth = barWidth / 4;
        dc.DrawRectangle(System.Windows.Media.Brushes.Black, null, new Rect(x, y, segmentWidth, barHeight));
        dc.DrawRectangle(System.Windows.Media.Brushes.Black, null, new Rect(x + segmentWidth * 2, y, segmentWidth, barHeight));
        
        // 눈금선
        dc.DrawLine(pen, new System.Windows.Point(x, y - 3), new System.Windows.Point(x, y + barHeight + 3));
        dc.DrawLine(pen, new System.Windows.Point(x + barWidth / 2, y - 3), new System.Windows.Point(x + barWidth / 2, y + barHeight + 3));
        dc.DrawLine(pen, new System.Windows.Point(x + barWidth, y - 3), new System.Windows.Point(x + barWidth, y + barHeight + 3));
        
        // 텍스트
        var typeface = new Typeface(new System.Windows.Media.FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        
        var text0 = new FormattedText("0", System.Globalization.CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight, typeface, 10, System.Windows.Media.Brushes.Black, 1.0);
        dc.DrawText(text0, new System.Windows.Point(x - 3, y + barHeight + 3));
        
        var textScale = new FormattedText("축척", System.Globalization.CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight, typeface, 11, System.Windows.Media.Brushes.Black, 1.0);
        dc.DrawText(textScale, new System.Windows.Point(x, y - 20));
    }
    
    /// <summary>
    /// 방위표 그리기
    /// </summary>
    private void DrawNorthArrow(DrawingContext dc, int width, int height)
    {
        var centerX = width - 60.0;
        var centerY = 60.0;
        var outerSize = 40.0;
        var innerSize = 25.0;
        
        // 배경 원 (반투명 흰색)
        dc.DrawEllipse(
            new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 255, 255, 255)), 
            new System.Windows.Media.Pen(System.Windows.Media.Brushes.DarkGray, 2), 
            new System.Windows.Point(centerX, centerY), outerSize, outerSize);
        
        // 내부 원
        dc.DrawEllipse(
            null, 
            new System.Windows.Media.Pen(System.Windows.Media.Brushes.Gray, 1), 
            new System.Windows.Point(centerX, centerY), innerSize, innerSize);
        
        // 북쪽 화살표 (검은색 삼각형)
        var northArrow = new StreamGeometry();
        using (var ctx = northArrow.Open())
        {
            ctx.BeginFigure(new System.Windows.Point(centerX, centerY - innerSize), true, true);
            ctx.LineTo(new System.Windows.Point(centerX - 10, centerY), true, false);
            ctx.LineTo(new System.Windows.Point(centerX, centerY - 5), true, false);
            ctx.LineTo(new System.Windows.Point(centerX + 10, centerY), true, false);
        }
        dc.DrawGeometry(System.Windows.Media.Brushes.Black, null, northArrow);
        
        // 남쪽 화살표 (흰색 삼각형)
        var southArrow = new StreamGeometry();
        using (var ctx = southArrow.Open())
        {
            ctx.BeginFigure(new System.Windows.Point(centerX, centerY + innerSize), true, true);
            ctx.LineTo(new System.Windows.Point(centerX - 10, centerY), true, false);
            ctx.LineTo(new System.Windows.Point(centerX, centerY + 5), true, false);
            ctx.LineTo(new System.Windows.Point(centerX + 10, centerY), true, false);
        }
        dc.DrawGeometry(System.Windows.Media.Brushes.White, new System.Windows.Media.Pen(System.Windows.Media.Brushes.Black, 1), southArrow);
        
        // N 텍스트
        var typeface = new Typeface(new System.Windows.Media.FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var textN = new FormattedText("N", System.Globalization.CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight, typeface, 16, System.Windows.Media.Brushes.Black, 1.0);
        dc.DrawText(textN, new System.Windows.Point(centerX - textN.Width / 2, centerY - outerSize - 20));
    }
    
    /// <summary>
    /// 범례 그리기
    /// </summary>
    private void DrawLegend(DrawingContext dc, int width, int height)
    {
        var x = 20.0;
        var y = 20.0;
        var itemHeight = 25.0;
        var boxSize = 16.0;
        var padding = 10.0;
        
        // 범례 제목
        var typeface = new Typeface(new System.Windows.Media.FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var titleText = new FormattedText("범례", System.Globalization.CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight, typeface, 12, System.Windows.Media.Brushes.Black, 1.0);
        
        // 배경 박스
        var bgRect = new Rect(x - padding, y - padding, 150, itemHeight + padding * 2 + titleText.Height);
        dc.DrawRectangle(
            new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 255, 255, 255)), 
            new System.Windows.Media.Pen(System.Windows.Media.Brushes.Gray, 1), 
            bgRect);
        
        dc.DrawText(titleText, new System.Windows.Point(x, y));
        
        // 샘플 범례 항목
        var normalTypeface = new Typeface(new System.Windows.Media.FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var itemY = y + titleText.Height + 5;
        
        dc.DrawRectangle(System.Windows.Media.Brushes.SteelBlue, 
            new System.Windows.Media.Pen(System.Windows.Media.Brushes.DarkBlue, 1), 
            new Rect(x, itemY, boxSize, boxSize));
        
        var itemText = new FormattedText("레이어", System.Globalization.CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight, normalTypeface, 11, System.Windows.Media.Brushes.Black, 1.0);
        dc.DrawText(itemText, new System.Windows.Point(x + boxSize + 8, itemY));
    }
    
    /// <summary>
    /// 제목 그리기
    /// </summary>
    private void DrawTitle(DrawingContext dc, int width, int height)
    {
        var title = "지도";
        var typeface = new Typeface(new System.Windows.Media.FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var titleText = new FormattedText(title, System.Globalization.CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight, typeface, 18, System.Windows.Media.Brushes.Black, 1.0);
        
        var x = (width - titleText.Width) / 2;
        var y = 15.0;
        
        // 배경
        var bgRect = new Rect(x - 15, y - 5, titleText.Width + 30, titleText.Height + 10);
        dc.DrawRectangle(
            new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 255, 255, 255)), 
            null, 
            bgRect);
        
        dc.DrawText(titleText, new System.Windows.Point(x, y));
    }
    
    /// <summary>
    /// 이미지로 내보내기
    /// </summary>
    private ExportResult ExportToImage(string filePath, RenderTargetBitmap bitmap, ExportFormat format)
    {
        BitmapEncoder encoder = format switch
        {
            ExportFormat.Jpeg => new JpegBitmapEncoder { QualityLevel = 95 },
            _ => new PngBitmapEncoder()
        };
        
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        
        using (var stream = File.Create(filePath))
        {
            encoder.Save(stream);
        }
        
        return new ExportResult
        {
            Success = true,
            FilePath = filePath
        };
    }
    
    /// <summary>
    /// PDF로 내보내기 (간단한 구현 - 이미지를 PDF에 포함)
    /// </summary>
    private ExportResult ExportToPdf(string filePath, RenderTargetBitmap bitmap, int width, int height, int dpi)
    {
        // 먼저 임시 PNG 파일로 저장
        var tempPngPath = Path.Combine(Path.GetTempPath(), $"map_temp_{Guid.NewGuid()}.png");
        
        try
        {
            var pngEncoder = new PngBitmapEncoder();
            pngEncoder.Frames.Add(BitmapFrame.Create(bitmap));
            
            using (var stream = File.Create(tempPngPath))
            {
                pngEncoder.Save(stream);
            }
            
            // 간단한 PDF 생성 (XPS를 통한 변환 또는 직접 PDF 생성)
            // 여기서는 간단히 이미지를 포함하는 PDF를 생성
            CreateSimplePdf(filePath, tempPngPath, width, height, dpi);
            
            return new ExportResult
            {
                Success = true,
                FilePath = filePath
            };
        }
        finally
        {
            // 임시 파일 삭제
            if (File.Exists(tempPngPath))
            {
                try { File.Delete(tempPngPath); } catch { }
            }
        }
    }
    
    /// <summary>
    /// 간단한 PDF 생성 (이미지 포함)
    /// </summary>
    private void CreateSimplePdf(string pdfPath, string imagePath, int width, int height, int dpi)
    {
        // PDF 직접 생성 (간단한 구현)
        // 실제 프로덕션에서는 PdfSharp, iTextSharp 등의 라이브러리 사용 권장
        
        var imageBytes = File.ReadAllBytes(imagePath);
        var imageBase64 = Convert.ToBase64String(imageBytes);
        
        // 페이지 크기 계산 (포인트 단위, 1인치 = 72포인트)
        double pageWidth = width * 72.0 / dpi;
        double pageHeight = height * 72.0 / dpi;
        
        // 간단한 PDF 구조 생성
        using (var writer = new StreamWriter(pdfPath))
        {
            writer.WriteLine("%PDF-1.4");
            writer.WriteLine("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj");
            writer.WriteLine($"2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj");
            writer.WriteLine($"3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidth:F2} {pageHeight:F2}] /Contents 4 0 R /Resources << /XObject << /Im0 5 0 R >> >> >> endobj");
            
            // 컨텐츠 스트림
            var contentStream = $"q {pageWidth:F2} 0 0 {pageHeight:F2} 0 0 cm /Im0 Do Q";
            writer.WriteLine($"4 0 obj << /Length {contentStream.Length} >> stream");
            writer.WriteLine(contentStream);
            writer.WriteLine("endstream endobj");
            
            // 이미지 객체 (PNG)
            writer.WriteLine($"5 0 obj << /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {imageBytes.Length} >> stream");
            writer.Flush();
        }
        
        // 바이너리 이미지 데이터 추가
        using (var stream = new FileStream(pdfPath, FileMode.Append))
        {
            stream.Write(imageBytes, 0, imageBytes.Length);
        }
        
        using (var writer = new StreamWriter(pdfPath, true))
        {
            writer.WriteLine();
            writer.WriteLine("endstream endobj");
            writer.WriteLine("xref");
            writer.WriteLine("0 6");
            writer.WriteLine("0000000000 65535 f ");
            writer.WriteLine("0000000009 00000 n ");
            writer.WriteLine("0000000058 00000 n ");
            writer.WriteLine("0000000115 00000 n ");
            writer.WriteLine("trailer << /Size 6 /Root 1 0 R >>");
            writer.WriteLine("startxref");
            writer.WriteLine("0");
            writer.WriteLine("%%EOF");
        }
    }
    
    /// <summary>
    /// 취소 버튼 클릭
    /// </summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
