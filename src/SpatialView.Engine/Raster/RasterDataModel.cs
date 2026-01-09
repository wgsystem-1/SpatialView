using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Raster;

/// <summary>
/// 래스터 데이터 모델
/// </summary>
public class RasterDataset : IDisposable
{
    private bool _disposed = false;

    /// <summary>
    /// 래스터 너비 (픽셀 수)
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 래스터 높이 (픽셀 수)
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 밴드 수
    /// </summary>
    public int BandCount => Bands.Count;

    /// <summary>
    /// 래스터 밴드들
    /// </summary>
    public List<RasterBand> Bands { get; set; } = new();

    /// <summary>
    /// 지리적 변환 정보
    /// </summary>
    public GeoTransform GeoTransform { get; set; } = new();

    /// <summary>
    /// 공간 참조 시스템
    /// </summary>
    public string? SpatialReference { get; set; }

    /// <summary>
    /// 메타데이터
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// No Data 값
    /// </summary>
    public double? NoDataValue { get; set; }

    /// <summary>
    /// 래스터의 지리적 범위 계산
    /// </summary>
    public Envelope GetBounds()
    {
        var minX = GeoTransform.OriginX;
        var maxX = GeoTransform.OriginX + Width * GeoTransform.PixelWidth;
        var maxY = GeoTransform.OriginY;
        var minY = GeoTransform.OriginY + Height * GeoTransform.PixelHeight;

        return new Envelope(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// 픽셀 좌표를 지리적 좌표로 변환
    /// </summary>
    public ICoordinate PixelToGeo(int pixelX, int pixelY)
    {
        var geoX = GeoTransform.OriginX + pixelX * GeoTransform.PixelWidth + 
                   pixelY * GeoTransform.XSkew;
        var geoY = GeoTransform.OriginY + pixelX * GeoTransform.YSkew + 
                   pixelY * GeoTransform.PixelHeight;

        return new Coordinate(geoX, geoY);
    }

    /// <summary>
    /// 지리적 좌표를 픽셀 좌표로 변환
    /// </summary>
    public (int pixelX, int pixelY) GeoToPixel(double geoX, double geoY)
    {
        var deltaX = geoX - GeoTransform.OriginX;
        var deltaY = geoY - GeoTransform.OriginY;

        var denominator = GeoTransform.PixelWidth * GeoTransform.PixelHeight - 
                         GeoTransform.XSkew * GeoTransform.YSkew;

        var pixelX = (int)((deltaX * GeoTransform.PixelHeight - deltaY * GeoTransform.XSkew) / denominator);
        var pixelY = (int)((deltaY * GeoTransform.PixelWidth - deltaX * GeoTransform.YSkew) / denominator);

        return (pixelX, pixelY);
    }

    /// <summary>
    /// 특정 위치의 픽셀 값 가져오기
    /// </summary>
    public double? GetPixelValue(int bandIndex, int x, int y)
    {
        if (bandIndex < 0 || bandIndex >= Bands.Count) return null;
        return Bands[bandIndex].GetValue(x, y);
    }

    /// <summary>
    /// 특정 위치의 픽셀 값 설정
    /// </summary>
    public void SetPixelValue(int bandIndex, int x, int y, double value)
    {
        if (bandIndex >= 0 && bandIndex < Bands.Count)
        {
            Bands[bandIndex].SetValue(x, y, value);
        }
    }

    /// <summary>
    /// 새 밴드 추가
    /// </summary>
    public void AddBand(RasterDataType dataType, string? name = null)
    {
        var band = new RasterBand(Width, Height, dataType, name ?? $"Band_{Bands.Count + 1}");
        Bands.Add(band);
    }

    /// <summary>
    /// 밴드 제거
    /// </summary>
    public bool RemoveBand(int index)
    {
        if (index >= 0 && index < Bands.Count)
        {
            Bands[index].Dispose();
            Bands.RemoveAt(index);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 서브셋 추출 (특정 영역의 래스터 데이터)
    /// </summary>
    public RasterDataset ExtractSubset(int startX, int startY, int width, int height)
    {
        if (startX < 0 || startY < 0 || startX + width > Width || startY + height > Height)
            throw new ArgumentOutOfRangeException("Subset bounds exceed raster dimensions");

        var subset = new RasterDataset
        {
            Width = width,
            Height = height,
            SpatialReference = SpatialReference,
            NoDataValue = NoDataValue
        };

        // 지리적 변환 정보 조정
        var originPixel = PixelToGeo(startX, startY);
        subset.GeoTransform = new GeoTransform
        {
            OriginX = originPixel.X,
            OriginY = originPixel.Y,
            PixelWidth = GeoTransform.PixelWidth,
            PixelHeight = GeoTransform.PixelHeight,
            XSkew = GeoTransform.XSkew,
            YSkew = GeoTransform.YSkew
        };

        // 각 밴드의 서브셋 생성
        foreach (var band in Bands)
        {
            var subsetBand = band.ExtractSubset(startX, startY, width, height);
            subset.Bands.Add(subsetBand);
        }

        return subset;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var band in Bands)
            {
                band?.Dispose();
            }
            Bands.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// 래스터 밴드
/// </summary>
public class RasterBand : IDisposable
{
    private double[,]? _data;
    private bool _disposed = false;

    public int Width { get; }
    public int Height { get; }
    public RasterDataType DataType { get; }
    public string Name { get; set; }
    public double? NoDataValue { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// 밴드 통계 정보
    /// </summary>
    public RasterStatistics? Statistics { get; set; }

    /// <summary>
    /// 컬러 테이블 (팔레트 이미지용)
    /// </summary>
    public ColorTable? ColorTable { get; set; }

    public RasterBand(int width, int height, RasterDataType dataType, string name)
    {
        Width = width;
        Height = height;
        DataType = dataType;
        Name = name;
        _data = new double[height, width];
    }

    /// <summary>
    /// 픽셀 값 가져오기
    /// </summary>
    public double? GetValue(int x, int y)
    {
        if (_disposed || _data == null) return null;
        if (x < 0 || x >= Width || y < 0 || y >= Height) return null;

        var value = _data[y, x];
        return NoDataValue.HasValue && Math.Abs(value - NoDataValue.Value) < 1e-10 ? null : value;
    }

    /// <summary>
    /// 픽셀 값 설정
    /// </summary>
    public void SetValue(int x, int y, double value)
    {
        if (_disposed || _data == null) return;
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;

        _data[y, x] = value;
        
        // 통계 정보 무효화
        Statistics = null;
    }

    /// <summary>
    /// 전체 데이터 배열 가져오기
    /// </summary>
    public double[,]? GetData()
    {
        return _data;
    }

    /// <summary>
    /// 전체 데이터 배열 설정
    /// </summary>
    public void SetData(double[,] data)
    {
        if (data.GetLength(0) != Height || data.GetLength(1) != Width)
            throw new ArgumentException("Data dimensions don't match band dimensions");

        _data = data;
        Statistics = null;
    }

    /// <summary>
    /// 블록 단위로 데이터 읽기
    /// </summary>
    public double[,]? ReadBlock(int startX, int startY, int blockWidth, int blockHeight)
    {
        if (_disposed || _data == null) return null;
        if (startX < 0 || startY < 0 || startX + blockWidth > Width || startY + blockHeight > Height)
            return null;

        var block = new double[blockHeight, blockWidth];
        for (int y = 0; y < blockHeight; y++)
        {
            for (int x = 0; x < blockWidth; x++)
            {
                block[y, x] = _data[startY + y, startX + x];
            }
        }

        return block;
    }

    /// <summary>
    /// 블록 단위로 데이터 쓰기
    /// </summary>
    public void WriteBlock(int startX, int startY, double[,] block)
    {
        if (_disposed || _data == null) return;
        
        int blockHeight = block.GetLength(0);
        int blockWidth = block.GetLength(1);
        
        if (startX < 0 || startY < 0 || startX + blockWidth > Width || startY + blockHeight > Height)
            return;

        for (int y = 0; y < blockHeight; y++)
        {
            for (int x = 0; x < blockWidth; x++)
            {
                _data[startY + y, startX + x] = block[y, x];
            }
        }

        Statistics = null;
    }

    /// <summary>
    /// 통계 정보 계산
    /// </summary>
    public RasterStatistics CalculateStatistics()
    {
        if (_disposed || _data == null) 
            return new RasterStatistics();

        var values = new List<double>();
        double sum = 0;
        double min = double.MaxValue;
        double max = double.MinValue;
        int validCount = 0;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var value = _data[y, x];
                
                // NoData 값 제외
                if (NoDataValue.HasValue && Math.Abs(value - NoDataValue.Value) < 1e-10)
                    continue;

                values.Add(value);
                sum += value;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
                validCount++;
            }
        }

        var statistics = new RasterStatistics
        {
            ValidCount = validCount,
            NoDataCount = Width * Height - validCount,
            Minimum = validCount > 0 ? min : double.NaN,
            Maximum = validCount > 0 ? max : double.NaN,
            Mean = validCount > 0 ? sum / validCount : double.NaN
        };

        if (validCount > 1)
        {
            var variance = values.Sum(v => Math.Pow(v - statistics.Mean, 2)) / (validCount - 1);
            statistics.StandardDeviation = Math.Sqrt(variance);
            
            values.Sort();
            statistics.Median = validCount % 2 == 0 
                ? (values[validCount / 2 - 1] + values[validCount / 2]) / 2
                : values[validCount / 2];
        }

        Statistics = statistics;
        return statistics;
    }

    /// <summary>
    /// 서브셋 추출
    /// </summary>
    public RasterBand ExtractSubset(int startX, int startY, int width, int height)
    {
        if (_disposed || _data == null)
            throw new ObjectDisposedException(nameof(RasterBand));

        var subset = new RasterBand(width, height, DataType, $"{Name}_subset")
        {
            NoDataValue = NoDataValue
        };

        var subsetData = new double[height, width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                subsetData[y, x] = _data[startY + y, startX + x];
            }
        }

        subset.SetData(subsetData);
        return subset;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _data = null;
            _disposed = true;
        }
    }
}

/// <summary>
/// 지리적 변환 정보
/// </summary>
public class GeoTransform
{
    /// <summary>
    /// 좌상단 X 좌표
    /// </summary>
    public double OriginX { get; set; }

    /// <summary>
    /// 픽셀 X 방향 크기
    /// </summary>
    public double PixelWidth { get; set; }

    /// <summary>
    /// X 방향 기울기 (회전)
    /// </summary>
    public double XSkew { get; set; }

    /// <summary>
    /// 좌상단 Y 좌표
    /// </summary>
    public double OriginY { get; set; }

    /// <summary>
    /// Y 방향 기울기 (회전)
    /// </summary>
    public double YSkew { get; set; }

    /// <summary>
    /// 픽셀 Y 방향 크기 (일반적으로 음수)
    /// </summary>
    public double PixelHeight { get; set; }

    /// <summary>
    /// GDAL 형식 변환 배열로 변환
    /// </summary>
    public double[] ToArray()
    {
        return new[] { OriginX, PixelWidth, XSkew, OriginY, YSkew, PixelHeight };
    }

    /// <summary>
    /// GDAL 형식 배열에서 생성
    /// </summary>
    public static GeoTransform FromArray(double[] transform)
    {
        if (transform?.Length != 6)
            throw new ArgumentException("Transform array must have 6 elements");

        return new GeoTransform
        {
            OriginX = transform[0],
            PixelWidth = transform[1],
            XSkew = transform[2],
            OriginY = transform[3],
            YSkew = transform[4],
            PixelHeight = transform[5]
        };
    }
}

/// <summary>
/// 래스터 데이터 타입
/// </summary>
public enum RasterDataType
{
    Byte,       // 8-bit unsigned integer
    Int16,      // 16-bit signed integer
    UInt16,     // 16-bit unsigned integer
    Int32,      // 32-bit signed integer
    UInt32,     // 32-bit unsigned integer
    Float32,    // 32-bit floating point
    Float64     // 64-bit floating point
}

/// <summary>
/// 래스터 통계 정보
/// </summary>
public class RasterStatistics
{
    public int ValidCount { get; set; }
    public int NoDataCount { get; set; }
    public double Minimum { get; set; }
    public double Maximum { get; set; }
    public double Mean { get; set; }
    public double Median { get; set; }
    public double StandardDeviation { get; set; }
}

/// <summary>
/// 컬러 테이블 (팔레트)
/// </summary>
public class ColorTable
{
    private readonly Dictionary<int, Color> _colors = new();

    /// <summary>
    /// 컬러 추가
    /// </summary>
    public void AddColor(int index, byte red, byte green, byte blue, byte alpha = 255)
    {
        _colors[index] = new Color { R = red, G = green, B = blue, A = alpha };
    }

    /// <summary>
    /// 컬러 가져오기
    /// </summary>
    public Color? GetColor(int index)
    {
        return _colors.TryGetValue(index, out var color) ? color : null;
    }

    /// <summary>
    /// 모든 컬러 엔트리
    /// </summary>
    public IReadOnlyDictionary<int, Color> Colors => _colors;
}

/// <summary>
/// 컬러 정보
/// </summary>
public struct Color
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public byte A { get; set; }

    public Color(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }
}

/// <summary>
/// 리샘플링 방법
/// </summary>
public enum ResamplingMethod
{
    NearestNeighbor,
    Bilinear,
    Cubic,
    CubicSpline,
    Lanczos,
    Average,
    Mode
}