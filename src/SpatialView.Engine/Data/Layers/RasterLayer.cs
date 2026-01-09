using SpatialView.Engine.Geometry;
using SpatialView.Engine.Raster;

namespace SpatialView.Engine.Data.Layers;

/// <summary>
/// 래스터 레이어 구현
/// </summary>
public class RasterLayer : BaseLayer, IRasterLayer
{
    private RasterDataset? _dataset;
    private bool _disposed = false;

    /// <summary>
    /// 래스터 데이터셋
    /// </summary>
    public RasterDataset? Dataset 
    { 
        get => _dataset;
        set
        {
            _dataset?.Dispose();
            _dataset = value;
            UpdateBounds();
        }
    }

    /// <summary>
    /// 래스터 파일 경로
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// 렌더링 스타일
    /// </summary>
    public RasterStyle Style { get; set; } = new();

    /// <summary>
    /// 투명도 (0.0 - 1.0)
    /// </summary>
    public double Opacity 
    { 
        get => Style.Opacity; 
        set => Style.Opacity = Math.Max(0.0, Math.Min(1.0, value)); 
    }

    /// <summary>
    /// 리샘플링 방법
    /// </summary>
    public ResamplingMethod ResamplingMethod { get; set; } = ResamplingMethod.NearestNeighbor;

    /// <summary>
    /// 밴드 조합 (RGB 렌더링용)
    /// </summary>
    public int[] BandCombination { get; set; } = { 0 }; // 기본값: 첫 번째 밴드만

    /// <summary>
    /// 대조비 향상 적용 여부
    /// </summary>
    public bool ContrastEnhancement { get; set; } = false;

    /// <summary>
    /// 히스토그램 평활화 적용 여부
    /// </summary>
    public bool HistogramEqualization { get; set; } = false;

    public RasterLayer(string name, string? filePath = null)
    {
        Name = name;
        FilePath = filePath;
    }
    
    public override Envelope? Extent => Dataset?.GetBounds();
    
    public override long FeatureCount => 0; // 래스터는 피처가 없음
    
    public override IEnumerable<IFeature> GetFeatures(Envelope? extent = null)
    {
        // 래스터 레이어는 피처를 반환하지 않음
        return Enumerable.Empty<IFeature>();
    }
    
    public override IEnumerable<IFeature> GetFeatures(IGeometry geometry)
    {
        // 래스터 레이어는 피처를 반환하지 않음
        return Enumerable.Empty<IFeature>();
    }
    
    public override void Refresh()
    {
        // 래스터 데이터 새로고침
        LoadAsync().Wait();
    }

    /// <summary>
    /// 래스터 데이터 로드
    /// </summary>
    public async Task<bool> LoadAsync()
    {
        if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
            return false;

        return await Task.Run(() =>
        {
            try
            {
                var format = RasterFormatDetector.DetectFormatFromContent(FilePath);
                
                switch (format)
                {
                    case RasterFormat.GeoTiff:
                        Dataset = GeoTiffIO.ReadGeoTiff(FilePath);
                        break;
                    default:
                        return false;
                }

                // IsLoaded = Dataset != null;
                return Dataset != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load raster: {ex.Message}");
                return false;
            }
        });
    }

    /// <summary>
    /// 특정 영역의 래스터 데이터 가져오기
    /// </summary>
    public RasterDataset? GetRasterData(Envelope? extent = null)
    {
        if (Dataset == null) return null;

        if (extent == null)
            return Dataset;

        // 요청된 범위에 해당하는 픽셀 영역 계산
        var (startX, startY) = Dataset.GeoToPixel(extent.MinX, extent.MaxY);
        var (endX, endY) = Dataset.GeoToPixel(extent.MaxX, extent.MinY);

        // 경계 확인 및 조정
        startX = Math.Max(0, Math.Min(startX, Dataset.Width - 1));
        startY = Math.Max(0, Math.Min(startY, Dataset.Height - 1));
        endX = Math.Max(0, Math.Min(endX, Dataset.Width - 1));
        endY = Math.Max(0, Math.Min(endY, Dataset.Height - 1));

        var width = Math.Abs(endX - startX) + 1;
        var height = Math.Abs(endY - startY) + 1;

        if (width <= 0 || height <= 0)
            return null;

        return Dataset.ExtractSubset(Math.Min(startX, endX), Math.Min(startY, endY), width, height);
    }

    /// <summary>
    /// 특정 위치의 픽셀 값 가져오기
    /// </summary>
    public Dictionary<int, double?> GetPixelValues(ICoordinate coordinate)
    {
        var result = new Dictionary<int, double?>();
        
        if (Dataset == null) return result;

        var (pixelX, pixelY) = Dataset.GeoToPixel(coordinate.X, coordinate.Y);

        for (int i = 0; i < Dataset.BandCount; i++)
        {
            result[i] = Dataset.GetPixelValue(i, pixelX, pixelY);
        }

        return result;
    }

    /// <summary>
    /// 래스터 통계 정보 계산
    /// </summary>
    public Dictionary<int, RasterStatistics> CalculateStatistics(bool forceRecalculate = false)
    {
        var result = new Dictionary<int, RasterStatistics>();
        
        if (Dataset == null) return result;

        for (int i = 0; i < Dataset.BandCount; i++)
        {
            var band = Dataset.Bands[i];
            if (band.Statistics == null || forceRecalculate)
            {
                result[i] = band.CalculateStatistics();
            }
            else
            {
                result[i] = band.Statistics;
            }
        }

        return result;
    }

    /// <summary>
    /// 컬러 매핑 적용
    /// </summary>
    public byte[,]? ApplyColorMap(int bandIndex, ColorMap colorMap)
    {
        if (Dataset == null || bandIndex >= Dataset.BandCount) return null;

        var band = Dataset.Bands[bandIndex];
        var data = band.GetData();
        if (data == null) return null;

        var result = new byte[band.Height, band.Width * 4]; // RGBA

        for (int y = 0; y < band.Height; y++)
        {
            for (int x = 0; x < band.Width; x++)
            {
                var value = data[y, x];
                var color = colorMap.GetColor(value);
                
                var baseIndex = x * 4;
                result[y, baseIndex] = color.R;     // R
                result[y, baseIndex + 1] = color.G; // G
                result[y, baseIndex + 2] = color.B; // B
                result[y, baseIndex + 3] = color.A; // A
            }
        }

        return result;
    }

    /// <summary>
    /// 히스토그램 계산
    /// </summary>
    public Histogram CalculateHistogram(int bandIndex, int bins = 256)
    {
        if (Dataset == null || bandIndex >= Dataset.BandCount)
            return new Histogram();

        var band = Dataset.Bands[bandIndex];
        var data = band.GetData();
        if (data == null) return new Histogram();

        var statistics = band.Statistics ?? band.CalculateStatistics();
        var binWidth = (statistics.Maximum - statistics.Minimum) / bins;
        var counts = new int[bins];

        for (int y = 0; y < band.Height; y++)
        {
            for (int x = 0; x < band.Width; x++)
            {
                var value = data[y, x];
                
                // NoData 값 제외
                if (band.NoDataValue.HasValue && Math.Abs(value - band.NoDataValue.Value) < 1e-10)
                    continue;

                var binIndex = (int)Math.Floor((value - statistics.Minimum) / binWidth);
                binIndex = Math.Max(0, Math.Min(bins - 1, binIndex));
                counts[binIndex]++;
            }
        }

        return new Histogram
        {
            BinCount = bins,
            BinWidth = binWidth,
            MinValue = statistics.Minimum,
            MaxValue = statistics.Maximum,
            Counts = counts
        };
    }

    /// <summary>
    /// 레이어 범위 업데이트
    /// </summary>
    private void UpdateBounds()
    {
        // Dataset의 경계 정보는 Dataset.GetBounds()로 직접 접근
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            Dataset?.Dispose();
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// 래스터 레이어 인터페이스
/// </summary>
public interface IRasterLayer : ILayer
{
    RasterDataset? Dataset { get; }
    string? FilePath { get; }
    RasterStyle Style { get; }
    ResamplingMethod ResamplingMethod { get; set; }
    int[] BandCombination { get; set; }

    Task<bool> LoadAsync();
    RasterDataset? GetRasterData(Envelope? extent = null);
    Dictionary<int, double?> GetPixelValues(ICoordinate coordinate);
    Dictionary<int, RasterStatistics> CalculateStatistics(bool forceRecalculate = false);
    Histogram CalculateHistogram(int bandIndex, int bins = 256);
}

/// <summary>
/// 래스터 렌더링 스타일
/// </summary>
public class RasterStyle
{
    /// <summary>
    /// 투명도 (0.0 - 1.0)
    /// </summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// 밝기 조정 (-100 - 100)
    /// </summary>
    public double Brightness { get; set; } = 0;

    /// <summary>
    /// 대비 조정 (-100 - 100)
    /// </summary>
    public double Contrast { get; set; } = 0;

    /// <summary>
    /// 감마 보정 (0.1 - 3.0)
    /// </summary>
    public double Gamma { get; set; } = 1.0;

    /// <summary>
    /// 색조 조정 (HSV)
    /// </summary>
    public double Hue { get; set; } = 0;

    /// <summary>
    /// 채도 조정 (HSV)
    /// </summary>
    public double Saturation { get; set; } = 0;

    /// <summary>
    /// 컬러 맵
    /// </summary>
    public ColorMap? ColorMap { get; set; }

    /// <summary>
    /// 밴드별 최소/최대 값 (스트레칭용)
    /// </summary>
    public Dictionary<int, (double min, double max)> ValueRanges { get; set; } = new();
}

/// <summary>
/// 컬러 매핑
/// </summary>
public class ColorMap
{
    private readonly List<ColorMapEntry> _entries = new();

    /// <summary>
    /// 컬러 매핑 엔트리 추가
    /// </summary>
    public void AddEntry(double value, byte r, byte g, byte b, byte a = 255)
    {
        _entries.Add(new ColorMapEntry { Value = value, Color = new Color(r, g, b, a) });
        _entries.Sort((x, y) => x.Value.CompareTo(y.Value));
    }

    /// <summary>
    /// 값에 해당하는 색상 가져오기 (보간 포함)
    /// </summary>
    public Color GetColor(double value)
    {
        if (_entries.Count == 0)
            return new Color(0, 0, 0);

        if (_entries.Count == 1)
            return _entries[0].Color;

        // 정확한 매치 찾기
        var exact = _entries.FirstOrDefault(e => Math.Abs(e.Value - value) < 1e-10);
        if (exact != null)
            return exact.Color;

        // 보간할 두 엔트리 찾기
        var lower = _entries.LastOrDefault(e => e.Value < value);
        var upper = _entries.FirstOrDefault(e => e.Value > value);

        if (lower == null) return _entries[0].Color;
        if (upper == null) return _entries[_entries.Count - 1].Color;

        // 선형 보간
        var ratio = (value - lower.Value) / (upper.Value - lower.Value);
        return InterpolateColor(lower.Color, upper.Color, ratio);
    }

    /// <summary>
    /// 색상 보간
    /// </summary>
    private static Color InterpolateColor(Color color1, Color color2, double ratio)
    {
        var r = (byte)(color1.R + (color2.R - color1.R) * ratio);
        var g = (byte)(color1.G + (color2.G - color1.G) * ratio);
        var b = (byte)(color1.B + (color2.B - color1.B) * ratio);
        var a = (byte)(color1.A + (color2.A - color1.A) * ratio);

        return new Color(r, g, b, a);
    }

    /// <summary>
    /// 미리 정의된 컬러맵들
    /// </summary>
    public static class Predefined
    {
        /// <summary>
        /// 그레이스케일 컬러맵
        /// </summary>
        public static ColorMap Grayscale(double min, double max)
        {
            var colorMap = new ColorMap();
            colorMap.AddEntry(min, 0, 0, 0);
            colorMap.AddEntry(max, 255, 255, 255);
            return colorMap;
        }

        /// <summary>
        /// 레인보우 컬러맵
        /// </summary>
        public static ColorMap Rainbow(double min, double max)
        {
            var colorMap = new ColorMap();
            var range = max - min;
            
            colorMap.AddEntry(min, 128, 0, 128);               // Purple
            colorMap.AddEntry(min + range * 0.25, 0, 0, 255);  // Blue
            colorMap.AddEntry(min + range * 0.5, 0, 255, 0);   // Green
            colorMap.AddEntry(min + range * 0.75, 255, 255, 0); // Yellow
            colorMap.AddEntry(max, 255, 0, 0);                 // Red
            
            return colorMap;
        }

        /// <summary>
        /// 지형 컬러맵
        /// </summary>
        public static ColorMap Terrain(double min, double max)
        {
            var colorMap = new ColorMap();
            var range = max - min;
            
            colorMap.AddEntry(min, 0, 0, 128);                  // Deep blue (water)
            colorMap.AddEntry(min + range * 0.2, 0, 128, 255); // Light blue
            colorMap.AddEntry(min + range * 0.4, 0, 255, 0);   // Green (low)
            colorMap.AddEntry(min + range * 0.6, 255, 255, 0); // Yellow
            colorMap.AddEntry(min + range * 0.8, 255, 128, 0); // Orange
            colorMap.AddEntry(max, 255, 255, 255);             // White (snow)
            
            return colorMap;
        }
    }
}

/// <summary>
/// 컬러맵 엔트리
/// </summary>
internal class ColorMapEntry
{
    public double Value { get; set; }
    public Color Color { get; set; }
}

/// <summary>
/// 히스토그램
/// </summary>
public class Histogram
{
    public int BinCount { get; set; }
    public double BinWidth { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public int[] Counts { get; set; } = Array.Empty<int>();

    /// <summary>
    /// 총 픽셀 수
    /// </summary>
    public int TotalCount => Counts.Sum();

    /// <summary>
    /// 최대 빈도
    /// </summary>
    public int MaxCount => Counts.Length > 0 ? Counts.Max() : 0;

    /// <summary>
    /// 특정 백분위수 값 계산
    /// </summary>
    public double GetPercentile(double percentile)
    {
        if (Counts.Length == 0) return 0;

        var targetCount = (int)(TotalCount * percentile / 100);
        var cumulative = 0;

        for (int i = 0; i < Counts.Length; i++)
        {
            cumulative += Counts[i];
            if (cumulative >= targetCount)
            {
                return MinValue + i * BinWidth;
            }
        }

        return MaxValue;
    }
}