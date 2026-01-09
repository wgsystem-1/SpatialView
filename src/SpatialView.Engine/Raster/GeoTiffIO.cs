using System.Text;

namespace SpatialView.Engine.Raster;

/// <summary>
/// GeoTIFF 파일 읽기/쓰기 구현
/// 간단한 TIFF 구조를 기반으로 한 기본적인 GeoTIFF 지원
/// </summary>
public static class GeoTiffIO
{
    /// <summary>
    /// GeoTIFF 파일 읽기
    /// </summary>
    /// <param name="filePath">파일 경로</param>
    /// <returns>래스터 데이터셋</returns>
    public static RasterDataset? ReadGeoTiff(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fileStream);

            // TIFF 헤더 읽기
            var header = ReadTiffHeader(reader);
            if (header == null) return null;

            // IFD (Image File Directory) 읽기
            var ifd = ReadImageFileDirectory(reader, header.IfdOffset, header.IsLittleEndian);
            if (ifd == null) return null;

            // 래스터 데이터셋 생성
            var dataset = new RasterDataset
            {
                Width = ifd.ImageWidth,
                Height = ifd.ImageLength,
                SpatialReference = ifd.SpatialReference,
                NoDataValue = ifd.NoDataValue
            };

            // GeoTransform 설정
            if (ifd.ModelPixelScale != null && ifd.ModelTiepoint != null)
            {
                dataset.GeoTransform = new GeoTransform
                {
                    OriginX = ifd.ModelTiepoint[3],
                    OriginY = ifd.ModelTiepoint[4],
                    PixelWidth = ifd.ModelPixelScale[0],
                    PixelHeight = -ifd.ModelPixelScale[1], // 일반적으로 음수
                    XSkew = 0,
                    YSkew = 0
                };
            }

            // 밴드 데이터 읽기
            var samplesPerPixel = ifd.SamplesPerPixel;
            for (int i = 0; i < samplesPerPixel; i++)
            {
                var band = ReadBandData(reader, ifd, i, header.IsLittleEndian);
                if (band != null)
                {
                    dataset.Bands.Add(band);
                }
            }

            return dataset;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Error reading GeoTIFF file: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// GeoTIFF 파일 쓰기
    /// </summary>
    /// <param name="dataset">래스터 데이터셋</param>
    /// <param name="filePath">출력 파일 경로</param>
    public static void WriteGeoTiff(RasterDataset dataset, string filePath)
    {
        if (dataset == null) throw new ArgumentNullException(nameof(dataset));
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be empty", nameof(filePath));

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(fileStream);

            // TIFF 헤더 쓰기
            WriteTiffHeader(writer);

            // IFD 위치 예약 (나중에 업데이트)
            var ifdPositionOffset = writer.BaseStream.Position - 4;

            // 이미지 데이터 쓰기
            var stripOffsets = new List<uint>();
            var stripByteCounts = new List<uint>();

            foreach (var band in dataset.Bands)
            {
                var (offsets, byteCounts) = WriteBandData(writer, band);
                stripOffsets.AddRange(offsets);
                stripByteCounts.AddRange(byteCounts);
            }

            // IFD 생성 및 쓰기
            var ifdOffset = (uint)writer.BaseStream.Position;
            WriteImageFileDirectory(writer, dataset, stripOffsets, stripByteCounts);

            // IFD 오프셋 업데이트
            writer.BaseStream.Seek(ifdPositionOffset, SeekOrigin.Begin);
            writer.Write(ifdOffset);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error writing GeoTIFF file: {ex.Message}", ex);
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// TIFF 헤더 읽기
    /// </summary>
    private static TiffHeader? ReadTiffHeader(BinaryReader reader)
    {
        var byteOrder = reader.ReadUInt16();
        bool isLittleEndian = byteOrder == 0x4949; // "II"
        
        if (!isLittleEndian && byteOrder != 0x4D4D) // "MM"
            return null;

        var magic = ReadUInt16(reader, isLittleEndian);
        if (magic != 42) return null; // TIFF magic number

        var ifdOffset = ReadUInt32(reader, isLittleEndian);

        return new TiffHeader
        {
            IsLittleEndian = isLittleEndian,
            IfdOffset = ifdOffset
        };
    }

    /// <summary>
    /// TIFF 헤더 쓰기
    /// </summary>
    private static void WriteTiffHeader(BinaryWriter writer)
    {
        writer.Write((ushort)0x4949); // Little endian
        writer.Write((ushort)42);     // TIFF magic number
        writer.Write((uint)0);        // IFD offset (placeholder)
    }

    /// <summary>
    /// Image File Directory 읽기
    /// </summary>
    private static ImageFileDirectory? ReadImageFileDirectory(BinaryReader reader, uint offset, bool isLittleEndian)
    {
        reader.BaseStream.Seek(offset, SeekOrigin.Begin);

        var entryCount = ReadUInt16(reader, isLittleEndian);
        var ifd = new ImageFileDirectory();

        for (int i = 0; i < entryCount; i++)
        {
            var tag = ReadUInt16(reader, isLittleEndian);
            var type = ReadUInt16(reader, isLittleEndian);
            var count = ReadUInt32(reader, isLittleEndian);
            var valueOffset = ReadUInt32(reader, isLittleEndian);

            ProcessIfdEntry(reader, ifd, tag, type, count, valueOffset, isLittleEndian);
        }

        return ifd;
    }

    /// <summary>
    /// Image File Directory 쓰기
    /// </summary>
    private static void WriteImageFileDirectory(BinaryWriter writer, RasterDataset dataset, 
        List<uint> stripOffsets, List<uint> stripByteCounts)
    {
        var entries = new List<IfdEntry>();

        // 필수 태그들
        entries.Add(new IfdEntry { Tag = 256, Type = 4, Count = 1, Value = (uint)dataset.Width }); // ImageWidth
        entries.Add(new IfdEntry { Tag = 257, Type = 4, Count = 1, Value = (uint)dataset.Height }); // ImageLength
        entries.Add(new IfdEntry { Tag = 258, Type = 3, Count = 1, Value = 64 }); // BitsPerSample (Float64)
        entries.Add(new IfdEntry { Tag = 259, Type = 3, Count = 1, Value = 1 }); // Compression (None)
        entries.Add(new IfdEntry { Tag = 262, Type = 3, Count = 1, Value = 1 }); // PhotometricInterpretation
        entries.Add(new IfdEntry { Tag = 277, Type = 3, Count = 1, Value = (uint)dataset.BandCount }); // SamplesPerPixel

        // GeoTIFF 태그들
        if (dataset.GeoTransform != null)
        {
            // ModelPixelScale
            var scaleOffset = WriteDoubleArray(writer, new[] { 
                dataset.GeoTransform.PixelWidth, 
                Math.Abs(dataset.GeoTransform.PixelHeight), 
                0.0 
            });
            entries.Add(new IfdEntry { Tag = 33550, Type = 12, Count = 3, Value = scaleOffset });

            // ModelTiepoint
            var tiepointOffset = WriteDoubleArray(writer, new[] { 
                0.0, 0.0, 0.0, 
                dataset.GeoTransform.OriginX, 
                dataset.GeoTransform.OriginY, 
                0.0 
            });
            entries.Add(new IfdEntry { Tag = 33922, Type = 12, Count = 6, Value = tiepointOffset });
        }

        // Strip 정보
        if (stripOffsets.Count > 0)
        {
            var offsetsOffset = WriteUInt32Array(writer, stripOffsets);
            entries.Add(new IfdEntry { Tag = 273, Type = 4, Count = (uint)stripOffsets.Count, Value = offsetsOffset });
            
            var byteCountsOffset = WriteUInt32Array(writer, stripByteCounts);
            entries.Add(new IfdEntry { Tag = 279, Type = 4, Count = (uint)stripByteCounts.Count, Value = byteCountsOffset });
        }

        // IFD 엔트리 개수 쓰기
        writer.Write((ushort)entries.Count);

        // IFD 엔트리들 쓰기
        foreach (var entry in entries)
        {
            writer.Write(entry.Tag);
            writer.Write(entry.Type);
            writer.Write(entry.Count);
            writer.Write(entry.Value);
        }

        // 다음 IFD 오프셋 (0 = 없음)
        writer.Write((uint)0);
    }

    /// <summary>
    /// IFD 엔트리 처리
    /// </summary>
    private static void ProcessIfdEntry(BinaryReader reader, ImageFileDirectory ifd, 
        ushort tag, ushort type, uint count, uint valueOffset, bool isLittleEndian)
    {
        var currentPos = reader.BaseStream.Position;

        try
        {
            switch (tag)
            {
                case 256: // ImageWidth
                    ifd.ImageWidth = (int)valueOffset;
                    break;
                case 257: // ImageLength
                    ifd.ImageLength = (int)valueOffset;
                    break;
                case 258: // BitsPerSample
                    ifd.BitsPerSample = (int)valueOffset;
                    break;
                case 277: // SamplesPerPixel
                    ifd.SamplesPerPixel = (int)valueOffset;
                    break;
                case 33550: // ModelPixelScaleTag
                    ifd.ModelPixelScale = ReadDoubleArray(reader, valueOffset, count, isLittleEndian);
                    break;
                case 33922: // ModelTiepointTag
                    ifd.ModelTiepoint = ReadDoubleArray(reader, valueOffset, count, isLittleEndian);
                    break;
                case 273: // StripOffsets
                    ifd.StripOffsets = ReadUInt32Array(reader, valueOffset, count, isLittleEndian);
                    break;
                case 279: // StripByteCounts
                    ifd.StripByteCounts = ReadUInt32Array(reader, valueOffset, count, isLittleEndian);
                    break;
            }
        }
        finally
        {
            reader.BaseStream.Position = currentPos;
        }
    }

    /// <summary>
    /// 밴드 데이터 읽기
    /// </summary>
    private static RasterBand? ReadBandData(BinaryReader reader, ImageFileDirectory ifd, int bandIndex, bool isLittleEndian)
    {
        if (ifd.StripOffsets == null || ifd.StripByteCounts == null)
            return null;

        var band = new RasterBand(ifd.ImageWidth, ifd.ImageLength, RasterDataType.Float64, $"Band_{bandIndex + 1}");
        var data = new double[ifd.ImageLength, ifd.ImageWidth];

        // 스트립 단위로 데이터 읽기
        int currentRow = 0;
        for (int i = 0; i < ifd.StripOffsets.Length; i++)
        {
            reader.BaseStream.Seek(ifd.StripOffsets[i], SeekOrigin.Begin);
            var stripData = reader.ReadBytes((int)ifd.StripByteCounts[i]);

            // 바이트 데이터를 double로 변환
            var doubleCount = stripData.Length / 8;
            for (int j = 0; j < doubleCount && currentRow < ifd.ImageLength; j++)
            {
                var value = BitConverter.ToDouble(stripData, j * 8);
                if (!isLittleEndian)
                    value = ReverseBytes(value);

                int row = currentRow / ifd.ImageWidth;
                int col = currentRow % ifd.ImageWidth;
                
                if (row < ifd.ImageLength && col < ifd.ImageWidth)
                {
                    data[row, col] = value;
                }
                currentRow++;
            }
        }

        band.SetData(data);
        return band;
    }

    /// <summary>
    /// 밴드 데이터 쓰기
    /// </summary>
    private static (List<uint> offsets, List<uint> byteCounts) WriteBandData(BinaryWriter writer, RasterBand band)
    {
        var offsets = new List<uint>();
        var byteCounts = new List<uint>();
        var data = band.GetData();

        if (data != null)
        {
            var offset = (uint)writer.BaseStream.Position;
            offsets.Add(offset);

            var bytes = new List<byte>();
            for (int y = 0; y < band.Height; y++)
            {
                for (int x = 0; x < band.Width; x++)
                {
                    var value = data[y, x];
                    bytes.AddRange(BitConverter.GetBytes(value));
                }
            }

            writer.Write(bytes.ToArray());
            byteCounts.Add((uint)bytes.Count);
        }

        return (offsets, byteCounts);
    }

    /// <summary>
    /// Double 배열 읽기
    /// </summary>
    private static double[]? ReadDoubleArray(BinaryReader reader, uint offset, uint count, bool isLittleEndian)
    {
        if (count == 0) return null;

        reader.BaseStream.Seek(offset, SeekOrigin.Begin);
        var values = new double[count];

        for (int i = 0; i < count; i++)
        {
            values[i] = ReadDouble(reader, isLittleEndian);
        }

        return values;
    }

    /// <summary>
    /// UInt32 배열 읽기
    /// </summary>
    private static uint[]? ReadUInt32Array(BinaryReader reader, uint offset, uint count, bool isLittleEndian)
    {
        if (count == 0) return null;

        reader.BaseStream.Seek(offset, SeekOrigin.Begin);
        var values = new uint[count];

        for (int i = 0; i < count; i++)
        {
            values[i] = ReadUInt32(reader, isLittleEndian);
        }

        return values;
    }

    /// <summary>
    /// Double 배열 쓰기
    /// </summary>
    private static uint WriteDoubleArray(BinaryWriter writer, double[] values)
    {
        var offset = (uint)writer.BaseStream.Position;
        foreach (var value in values)
        {
            writer.Write(value);
        }
        return offset;
    }

    /// <summary>
    /// UInt32 배열 쓰기
    /// </summary>
    private static uint WriteUInt32Array(BinaryWriter writer, List<uint> values)
    {
        var offset = (uint)writer.BaseStream.Position;
        foreach (var value in values)
        {
            writer.Write(value);
        }
        return offset;
    }

    /// <summary>
    /// Endian 고려하여 UInt16 읽기
    /// </summary>
    private static ushort ReadUInt16(BinaryReader reader, bool isLittleEndian)
    {
        var value = reader.ReadUInt16();
        return isLittleEndian ? value : ReverseBytes(value);
    }

    /// <summary>
    /// Endian 고려하여 UInt32 읽기
    /// </summary>
    private static uint ReadUInt32(BinaryReader reader, bool isLittleEndian)
    {
        var value = reader.ReadUInt32();
        return isLittleEndian ? value : ReverseBytes(value);
    }

    /// <summary>
    /// Endian 고려하여 Double 읽기
    /// </summary>
    private static double ReadDouble(BinaryReader reader, bool isLittleEndian)
    {
        var value = reader.ReadDouble();
        return isLittleEndian ? value : ReverseBytes(value);
    }

    /// <summary>
    /// UInt16 바이트 순서 뒤집기
    /// </summary>
    private static ushort ReverseBytes(ushort value)
    {
        return (ushort)((value >> 8) | (value << 8));
    }

    /// <summary>
    /// UInt32 바이트 순서 뒤집기
    /// </summary>
    private static uint ReverseBytes(uint value)
    {
        return ((value >> 24) & 0xFF) |
               (((value >> 16) & 0xFF) << 8) |
               (((value >> 8) & 0xFF) << 16) |
               ((value & 0xFF) << 24);
    }

    /// <summary>
    /// Double 바이트 순서 뒤집기
    /// </summary>
    private static double ReverseBytes(double value)
    {
        var bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }

    #endregion

    #region Supporting Classes

    /// <summary>
    /// TIFF 헤더 정보
    /// </summary>
    private class TiffHeader
    {
        public bool IsLittleEndian { get; set; }
        public uint IfdOffset { get; set; }
    }

    /// <summary>
    /// Image File Directory 정보
    /// </summary>
    private class ImageFileDirectory
    {
        public int ImageWidth { get; set; }
        public int ImageLength { get; set; }
        public int BitsPerSample { get; set; } = 64;
        public int SamplesPerPixel { get; set; } = 1;
        public double[]? ModelPixelScale { get; set; }
        public double[]? ModelTiepoint { get; set; }
        public uint[]? StripOffsets { get; set; }
        public uint[]? StripByteCounts { get; set; }
        public string? SpatialReference { get; set; }
        public double? NoDataValue { get; set; }
    }

    /// <summary>
    /// IFD 엔트리
    /// </summary>
    private class IfdEntry
    {
        public ushort Tag { get; set; }
        public ushort Type { get; set; }
        public uint Count { get; set; }
        public uint Value { get; set; }
    }

    #endregion
}

/// <summary>
/// 래스터 파일 포맷 감지 유틸리티
/// </summary>
public static class RasterFormatDetector
{
    /// <summary>
    /// 파일 확장자로부터 포맷 감지
    /// </summary>
    public static RasterFormat DetectFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
        
        return extension switch
        {
            ".tif" or ".tiff" => RasterFormat.GeoTiff,
            ".png" => RasterFormat.PNG,
            ".jpg" or ".jpeg" => RasterFormat.JPEG,
            ".bmp" => RasterFormat.BMP,
            ".asc" => RasterFormat.ArcGrid,
            ".hgt" => RasterFormat.SRTM,
            _ => RasterFormat.Unknown
        };
    }

    /// <summary>
    /// 파일 내용으로부터 포맷 감지
    /// </summary>
    public static RasterFormat DetectFormatFromContent(string filePath)
    {
        if (!File.Exists(filePath)) return RasterFormat.Unknown;

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fileStream);

            if (fileStream.Length < 8) return RasterFormat.Unknown;

            var header = reader.ReadBytes(8);

            // TIFF 매직 넘버 체크
            if ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00) ||
                (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A))
            {
                return RasterFormat.GeoTiff;
            }

            // PNG 매직 넘버 체크
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                return RasterFormat.PNG;
            }

            // JPEG 매직 넘버 체크
            if (header[0] == 0xFF && header[1] == 0xD8)
            {
                return RasterFormat.JPEG;
            }

            return DetectFormat(filePath); // 확장자 기반 감지로 폴백
        }
        catch
        {
            return DetectFormat(filePath);
        }
    }
}

/// <summary>
/// 지원하는 래스터 포맷
/// </summary>
public enum RasterFormat
{
    Unknown,
    GeoTiff,
    PNG,
    JPEG,
    BMP,
    ArcGrid,
    SRTM
}