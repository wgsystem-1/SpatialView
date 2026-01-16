using System.Text;
using SpatialView.Core.Enums;
using SpatialView.Core.Models;
using SpatialView.Core.Services.Interfaces;
using SpatialView.Engine.Data.Sources;

namespace SpatialView.Infrastructure.DataProviders;

/// <summary>
/// Shapefile (.shp) 데이터 Provider
/// SharpMap.Data.Providers.ShapeFile을 사용하여 로드합니다.
/// </summary>
public class ShapefileDataProvider : IDataProvider
{
    public string[] SupportedExtensions => new[] { ".shp" };
    
    public string ProviderName => "Shapefile";
    
    public bool IsFolderBased => false;

    /// <summary>
    /// 외부에서 강제로 지정할 DBF 인코딩(환경변수보다 우선 적용).
    /// </summary>
    public Encoding? ForcedEncoding { get; set; }
    
    /// <summary>
    /// 해당 파일을 로드할 수 있는지 확인
    /// </summary>
    public bool CanLoad(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;
        
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }
    
    /// <summary>
    /// Shapefile을 비동기로 로드
    /// </summary>
    public async Task<LayerInfo> LoadAsync(string filePath)
    {
        return await Task.Run(() => LoadInternal(filePath));
    }
    
    /// <summary>
    /// Shapefile 로드 내부 로직
    /// </summary>
    private LayerInfo LoadInternal(string filePath)
    {
        // 인코딩 등록 (CP949/EUC-KR 지원)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        try
        {
            // 자체 구현된 ShapefileDataSource 사용
            var shapefileDataSource = new ShapefileDataSource(filePath);
            
            // 레이어 이름은 파일명에서 추출
            var layerName = Path.GetFileNameWithoutExtension(filePath);
            
            // 범위 가져오기
            var extent = shapefileDataSource.GetExtent();
            
            // Engine의 VectorLayer 생성
            var vectorLayer = new Engine.Data.Layers.VectorLayer
            {
                Name = layerName,
                DataSource = shapefileDataSource,
                TableName = layerName,  // TableName 설정 (LoadFeatures에서 필요)
                Extent = extent  // 범위 직접 설정
            };
            
            // 지오메트리 타입 결정
            var geometryType = DetermineGeometryTypeFromDataSource(shapefileDataSource);
            
            // 좌표계 감지 (.prj 파일)
            var crs = DetectCRS(filePath);
            
            // 피처 개수
            var featureCount = (int)shapefileDataSource.GetFeatureCount();
            
            // IFeatureSource 어댑터 생성
            var featureSource = new Infrastructure.GisEngine.ShapefileFeatureSourceAdapter(shapefileDataSource);
            
            // SpatialViewVectorLayerAdapter에 FeatureSource 설정
            var layerAdapter = new Infrastructure.GisEngine.SpatialViewVectorLayerAdapter(vectorLayer)
            {
                DataSource = featureSource
                // Provider는 IDataProvider(GisEngine) 타입이 필요하므로 여기서 설정하지 않음
            };

            return new LayerInfo
            {
                Id = Guid.NewGuid(),
                Name = layerName,
                FilePath = filePath,
                GeometryType = geometryType,
                FeatureCount = featureCount,
                Extent = extent,
                CRS = crs,
                Layer = layerAdapter
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"자체 Shapefile 로더 실패: {ex.Message}");
            throw new InvalidOperationException($"Shapefile을 읽을 수 없습니다: {ex.Message}", ex);
        }
    }

    // Envelope 변환 메서드 제거 - Engine.Geometry.Envelope 직접 사용

    private GeometryType DetermineGeometryTypeFromDataSource(ShapefileDataSource dataSource)
    {
        // Shapefile 헤더에서 직접 지오메트리 타입 읽기
        var shapeType = dataSource.GeometryShapeType;
        
        return shapeType switch
        {
            // 2D 타입
            Engine.Data.Sources.ShapeType.Point => GeometryType.Point,
            Engine.Data.Sources.ShapeType.MultiPoint => GeometryType.MultiPoint,
            Engine.Data.Sources.ShapeType.PolyLine => GeometryType.LineString,
            Engine.Data.Sources.ShapeType.Polygon => GeometryType.Polygon,
            
            // Z (3D) 타입
            Engine.Data.Sources.ShapeType.PointZ => GeometryType.PointZ,
            Engine.Data.Sources.ShapeType.MultiPointZ => GeometryType.MultiPointZ,
            Engine.Data.Sources.ShapeType.PolyLineZ => GeometryType.LineStringZ,
            Engine.Data.Sources.ShapeType.PolygonZ => GeometryType.PolygonZ,
            
            // M (Measure) 타입
            Engine.Data.Sources.ShapeType.PointM => GeometryType.PointM,
            Engine.Data.Sources.ShapeType.MultiPointM => GeometryType.MultiPointM,
            Engine.Data.Sources.ShapeType.PolyLineM => GeometryType.LineStringM,
            Engine.Data.Sources.ShapeType.PolygonM => GeometryType.PolygonM,
            
            Engine.Data.Sources.ShapeType.NullShape => GeometryType.None,
            _ => GeometryType.Unknown
        };
    }
    
    /// <summary>
    /// Shapefile 구성 파일 존재 확인
    /// </summary>
    private void ValidateShapefileComponents(string shpFilePath)
    {
        if (!File.Exists(shpFilePath))
            throw new FileNotFoundException($"Shapefile을 찾을 수 없습니다: {shpFilePath}");
        
        var basePath = Path.ChangeExtension(shpFilePath, null);
        
        var shxPath = basePath + ".shx";
        if (!File.Exists(shxPath))
        {
            // 대소문자 변환 시도
            shxPath = basePath + ".SHX";
            if (!File.Exists(shxPath))
                throw new FileNotFoundException($".shx 파일을 찾을 수 없습니다: {Path.GetFileName(shpFilePath).Replace(".shp", ".shx")}");
        }
        
        var dbfPath = basePath + ".dbf";
        if (!File.Exists(dbfPath))
        {
            dbfPath = basePath + ".DBF";
            if (!File.Exists(dbfPath))
                throw new FileNotFoundException($".dbf 파일을 찾을 수 없습니다: {Path.GetFileName(shpFilePath).Replace(".shp", ".dbf")}");
        }
    }
    
    /// <summary>
    /// 사이드카(.encoding/.enc) > .cpg > DBF LDID 순으로 인코딩 감지
    /// </summary>
    private Encoding DetectEncoding(string shpFilePath)
    {
        var basePath = Path.ChangeExtension(shpFilePath, null);

        // 0. sidecar .encoding / .enc 파일 우선
        var encPath = basePath + ".encoding";
        if (!File.Exists(encPath))
            encPath = basePath + ".ENCODING";
        if (!File.Exists(encPath))
            encPath = basePath + ".enc";
        if (!File.Exists(encPath))
            encPath = basePath + ".ENC";

        if (File.Exists(encPath))
        {
            try
            {
                var name = File.ReadAllText(encPath).Trim();
                var enc = Encoding.GetEncoding(name);
                System.Diagnostics.Debug.WriteLine($"sidecar encoding 파일 적용: {name} => {enc.EncodingName} ({enc.CodePage})");
                return enc;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($".encoding/.enc 파싱 실패: {ex.Message}");
            }
        }
        
        // 1. CPG 파일에서 인코딩 확인
        var cpgPath = basePath + ".cpg";
        if (!File.Exists(cpgPath))
            cpgPath = basePath + ".CPG";
        
        if (File.Exists(cpgPath))
        {
            try
            {
                var encodingName = File.ReadAllText(cpgPath).Trim().ToUpperInvariant();
                System.Diagnostics.Debug.WriteLine($"CPG 파일 발견: {encodingName}");
                
                // 일반적인 코드페이지 매핑
                var encoding = encodingName switch
                {
                    "UTF-8" or "UTF8" or "65001" => Encoding.UTF8,
                    "EUC-KR" or "EUCKR" or "51949" => Encoding.GetEncoding(51949), // EUC-KR
                    "CP949" or "949" or "UHC" or "MS949" => Encoding.GetEncoding(949), // CP949 (Korean)
                    "CP1252" or "1252" or "ANSI" => Encoding.GetEncoding(1252), // Western European
                    "ISO-8859-1" or "LATIN1" => Encoding.GetEncoding("iso-8859-1"),
                    "BIG5" or "950" => Encoding.GetEncoding(950), // Traditional Chinese
                    "GB2312" or "936" => Encoding.GetEncoding(936), // Simplified Chinese
                    "SHIFT_JIS" or "932" => Encoding.GetEncoding(932), // Japanese
                    _ => TryGetEncodingByName(encodingName)
                };
                
                if (encoding != null)
                    return encoding;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CPG 인코딩 파싱 실패: {ex.Message}");
            }
        }
        
        // 2. DBF 파일의 Language Driver ID (LDID) 확인
        var dbfPath = basePath + ".dbf";
        if (!File.Exists(dbfPath))
            dbfPath = basePath + ".DBF";
        
        if (File.Exists(dbfPath))
        {
            try
            {
                using var fs = new FileStream(dbfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                // DBF 헤더의 29번째 바이트가 Language Driver ID
                if (fs.Length > 29)
                {
                    fs.Seek(29, SeekOrigin.Begin);
                    var ldid = fs.ReadByte();
                    
                    System.Diagnostics.Debug.WriteLine($"DBF LDID: 0x{ldid:X2} ({ldid})");
                    
                    // LDID to codepage mapping (일부)
                    var codePage = ldid switch
                    {
                        0x00 => 0,      // Unknown
                        0x01 => 437,    // DOS USA
                        0x02 => 850,    // DOS Multi-lingual
                        0x03 => 1252,   // Windows ANSI
                        0x57 => 1252,   // Windows ANSI (alternative)
                        0x58 => 1252,   // Western European ANSI
                        0x59 => 1252,   // Spanish ANSI
                        0x64 => 852,    // DOS Eastern European
                        0x65 => 866,    // DOS Russian
                        0x66 => 865,    // DOS Nordic
                        0x67 => 861,    // DOS Icelandic
                        0x68 => 895,    // DOS Kamenicky
                        0x69 => 620,    // DOS Mazovia
                        0x6A => 737,    // DOS Greek
                        0x6B => 857,    // DOS Turkish
                        0x78 => 950,    // Big5 (Traditional Chinese)
                        0x79 => 949,    // Korean (CP949) - 한글!
                        0x7A => 936,    // GBK (Simplified Chinese)
                        0x7B => 932,    // Japanese Shift-JIS
                        0x7C => 874,    // Thai
                        0x7D => 1255,   // Hebrew
                        0x7E => 1256,   // Arabic
                        0x86 => 737,    // Greek OEM
                        0x87 => 852,    // Slovenian OEM
                        0x88 => 857,    // Turkish OEM
                        0xC8 => 1250,   // Eastern European Windows
                        0xC9 => 1251,   // Russian Windows
                        0xCA => 1254,   // Turkish Windows
                        0xCB => 1253,   // Greek Windows
                        0xCC => 1257,   // Baltic Windows
                        _ => 0
                    };
                    
                    if (codePage > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"DBF LDID → CodePage: {codePage}");
                        return Encoding.GetEncoding(codePage);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DBF LDID 읽기 실패: {ex.Message}");
            }
        }
        
        // 3. 기본값: CP949 (한국어 Shapefile용 - EUC-KR의 확장)
        System.Diagnostics.Debug.WriteLine("기본 인코딩 사용: CP949");
        try
        {
            return Encoding.GetEncoding(949); // CP949는 EUC-KR의 상위 집합
        }
        catch
        {
            return Encoding.UTF8;
        }
    }
    
    /// <summary>
    /// 인코딩 이름으로 Encoding 객체 가져오기
    /// </summary>
    private Encoding? TryGetEncodingByName(string name)
    {
        try
        {
            // 숫자면 코드 페이지로 시도
            if (int.TryParse(name, out int codePage))
            {
                return Encoding.GetEncoding(codePage);
            }
            
            // 이름으로 시도
            return Encoding.GetEncoding(name);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// .prj 파일에서 좌표계 감지
    /// </summary>
    private string DetectCRS(string shpFilePath)
    {
        var basePath = Path.ChangeExtension(shpFilePath, null);
        var prjPath = basePath + ".prj";
        
        if (!File.Exists(prjPath))
            prjPath = basePath + ".PRJ";
        
        if (File.Exists(prjPath))
        {
            try
            {
                var wkt = File.ReadAllText(prjPath);
                System.Diagnostics.Debug.WriteLine($"PRJ 파일 내용: {wkt.Substring(0, Math.Min(200, wkt.Length))}...");
                
                // WKT에서 AUTHORITY["EPSG","코드"] 패턴으로 EPSG 코드 추출
                // 예: AUTHORITY["EPSG","5170"]
                var authorityMatch = System.Text.RegularExpressions.Regex.Match(
                    wkt, 
                    @"AUTHORITY\s*\[\s*""EPSG""\s*,\s*""?(\d+)""?\s*\]",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (authorityMatch.Success)
                {
                    var epsgCode = authorityMatch.Groups[1].Value;
                    System.Diagnostics.Debug.WriteLine($"EPSG 코드 감지: {epsgCode}");
                    return $"EPSG:{epsgCode}";
                }
                
                // 한국 좌표계 패턴 매칭 (AUTHORITY가 없는 경우)
                // Korea 2000 / Unified CS 시리즈 (EPSG:5170-5180)
                if (wkt.Contains("Korea_2000_Korea_Unified_CS") || wkt.Contains("Korea 2000 / Unified CS"))
                    return "EPSG:5179";
                
                if (wkt.Contains("Korea_2000") || wkt.Contains("Korean_2000") || wkt.Contains("Korea 2000"))
                {
                    // Central Belt (EPSG:5186), East Belt (5187), West Belt (5185) 등
                    if (wkt.Contains("Central_Belt") || wkt.Contains("Central Belt"))
                        return "EPSG:5186";
                    if (wkt.Contains("East_Belt") || wkt.Contains("East Belt"))
                        return "EPSG:5187";
                    if (wkt.Contains("West_Belt") || wkt.Contains("West Belt"))
                        return "EPSG:5185";
                    if (wkt.Contains("East_Sea_Belt") || wkt.Contains("East Sea Belt"))
                        return "EPSG:5188";
                    return "EPSG:5186"; // 기본값
                }
                
                // Korean 1985 (GRS80) 시리즈
                if (wkt.Contains("Korean_1985") || wkt.Contains("Korea_Central_Belt"))
                    return "EPSG:5174";
                
                // WGS84
                if (wkt.Contains("WGS_1984") || wkt.Contains("WGS 84") || wkt.Contains("GCS_WGS_1984"))
                    return "EPSG:4326";
                
                // UTM
                if (wkt.Contains("UTM_Zone_52N") || wkt.Contains("UTM zone 52N"))
                    return "EPSG:32652";
                if (wkt.Contains("UTM_Zone_51N") || wkt.Contains("UTM zone 51N"))
                    return "EPSG:32651";
                
                // Web Mercator
                if (wkt.Contains("Web_Mercator") || wkt.Contains("WGS_1984_Web_Mercator") || wkt.Contains("Pseudo-Mercator"))
                    return "EPSG:3857";
                
                // PROJCS 이름에서 직접 추출 시도
                var projcsMatch = System.Text.RegularExpressions.Regex.Match(wkt, @"PROJCS\s*\[\s*""([^""]+)""");
                if (projcsMatch.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"좌표계 이름: {projcsMatch.Groups[1].Value}");
                    return $"Unknown: {projcsMatch.Groups[1].Value}";
                }
                
                // 알 수 없는 좌표계
                return "Unknown (PRJ exists)";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PRJ 파싱 오류: {ex.Message}");
                return "Unknown";
            }
        }
        
        return "EPSG:4326"; // 기본값
    }
    
    // SharpMap 의존성 제거로 DetermineGeometryType(ShapeFile) 메서드 삭제
}
