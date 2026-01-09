using System.Collections.Concurrent;

namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// EPSG 코드 데이터베이스
/// 잘 알려진 좌표계들의 EPSG 코드와 정의를 관리합니다
/// </summary>
public class EPSGDatabase
{
    private static readonly Lazy<EPSGDatabase> _instance = new(() => new EPSGDatabase());
    
    /// <summary>
    /// 싱글톤 인스턴스
    /// </summary>
    public static EPSGDatabase Instance => _instance.Value;
    
    private readonly ConcurrentDictionary<int, EPSGEntry> _coordinateSystems;
    private readonly ConcurrentDictionary<string, int> _nameToSrid;
    
    /// <summary>
    /// 생성자
    /// </summary>
    private EPSGDatabase()
    {
        _coordinateSystems = new ConcurrentDictionary<int, EPSGEntry>();
        _nameToSrid = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        InitializeWellKnownSystems();
    }
    
    /// <summary>
    /// EPSG 코드로 좌표계 조회
    /// </summary>
    /// <param name="srid">EPSG 코드</param>
    /// <returns>좌표계 정보 또는 null</returns>
    public EPSGEntry? GetBySRID(int srid)
    {
        return _coordinateSystems.TryGetValue(srid, out var entry) ? entry : null;
    }
    
    /// <summary>
    /// 좌표계 이름으로 EPSG 코드 조회
    /// </summary>
    /// <param name="name">좌표계 이름</param>
    /// <returns>EPSG 코드 또는 null</returns>
    public int? GetSRIDByName(string name)
    {
        return _nameToSrid.TryGetValue(name, out var srid) ? srid : null;
    }
    
    /// <summary>
    /// 좌표계 이름으로 좌표계 조회
    /// </summary>
    /// <param name="name">좌표계 이름</param>
    /// <returns>좌표계 정보 또는 null</returns>
    public EPSGEntry? GetByName(string name)
    {
        var srid = GetSRIDByName(name);
        return srid.HasValue ? GetBySRID(srid.Value) : null;
    }
    
    /// <summary>
    /// 모든 등록된 좌표계 조회
    /// </summary>
    /// <returns>좌표계 목록</returns>
    public IEnumerable<EPSGEntry> GetAll()
    {
        return _coordinateSystems.Values.ToList();
    }
    
    /// <summary>
    /// 키워드로 좌표계 검색
    /// </summary>
    /// <param name="keyword">검색 키워드</param>
    /// <returns>매칭되는 좌표계 목록</returns>
    public IEnumerable<EPSGEntry> Search(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Enumerable.Empty<EPSGEntry>();
        
        var lowerKeyword = keyword.ToLowerInvariant();
        
        return _coordinateSystems.Values
            .Where(entry => 
                entry.Name.ToLowerInvariant().Contains(lowerKeyword) ||
                entry.Description.ToLowerInvariant().Contains(lowerKeyword) ||
                entry.SRID.ToString().Contains(lowerKeyword))
            .OrderBy(entry => entry.Name)
            .ToList();
    }
    
    /// <summary>
    /// 좌표계 타입으로 필터링
    /// </summary>
    /// <param name="type">좌표계 타입</param>
    /// <returns>해당 타입의 좌표계 목록</returns>
    public IEnumerable<EPSGEntry> GetByType(CoordinateSystemType type)
    {
        return _coordinateSystems.Values
            .Where(entry => entry.Type == type)
            .OrderBy(entry => entry.SRID)
            .ToList();
    }
    
    /// <summary>
    /// 지역별 좌표계 조회
    /// </summary>
    /// <param name="area">지역 이름</param>
    /// <returns>해당 지역의 좌표계 목록</returns>
    public IEnumerable<EPSGEntry> GetByArea(string area)
    {
        if (string.IsNullOrWhiteSpace(area))
            return Enumerable.Empty<EPSGEntry>();
        
        var lowerArea = area.ToLowerInvariant();
        
        return _coordinateSystems.Values
            .Where(entry => entry.AreaOfUse.ToLowerInvariant().Contains(lowerArea))
            .OrderBy(entry => entry.Name)
            .ToList();
    }
    
    /// <summary>
    /// 사용자 정의 좌표계 등록
    /// </summary>
    /// <param name="entry">좌표계 정보</param>
    /// <returns>등록 성공 여부</returns>
    public bool Register(EPSGEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));
        
        if (_coordinateSystems.TryAdd(entry.SRID, entry))
        {
            _nameToSrid.TryAdd(entry.Name, entry.SRID);
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 좌표계 등록 해제
    /// </summary>
    /// <param name="srid">EPSG 코드</param>
    /// <returns>해제 성공 여부</returns>
    public bool Unregister(int srid)
    {
        if (_coordinateSystems.TryRemove(srid, out var entry))
        {
            _nameToSrid.TryRemove(entry.Name, out _);
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 잘 알려진 좌표계들 초기화
    /// </summary>
    private void InitializeWellKnownSystems()
    {
        // WGS84 지리 좌표계
        Register(new EPSGEntry
        {
            SRID = 4326,
            Name = "WGS 84",
            Description = "World Geodetic System 1984",
            Type = CoordinateSystemType.Geographic,
            AreaOfUse = "World",
            WKT = @"GEOGCS[""WGS 84"",
    DATUM[""WGS_1984"",
        SPHEROID[""WGS 84"",6378137,298.257223563,
            AUTHORITY[""EPSG"",""7030""]],
        AUTHORITY[""EPSG"",""6326""]],
    PRIMEM[""Greenwich"",0,
        AUTHORITY[""EPSG"",""8901""]],
    UNIT[""degree"",0.0174532925199433,
        AUTHORITY[""EPSG"",""9122""]],
    AUTHORITY[""EPSG"",""4326""]]"
        });
        
        // Web Mercator
        Register(new EPSGEntry
        {
            SRID = 3857,
            Name = "WGS 84 / Pseudo-Mercator",
            Description = "Popular Visualisation CRS",
            Type = CoordinateSystemType.Projected,
            AreaOfUse = "World",
            WKT = @"PROJCS[""WGS 84 / Pseudo-Mercator"",
    GEOGCS[""WGS 84"",
        DATUM[""WGS_1984"",
            SPHEROID[""WGS 84"",6378137,298.257223563,
                AUTHORITY[""EPSG"",""7030""]],
            AUTHORITY[""EPSG"",""6326""]],
        PRIMEM[""Greenwich"",0,
            AUTHORITY[""EPSG"",""8901""]],
        UNIT[""degree"",0.0174532925199433,
            AUTHORITY[""EPSG"",""9122""]],
        AUTHORITY[""EPSG"",""4326""]],
    PROJECTION[""Mercator_1SP""],
    PARAMETER[""central_meridian"",0],
    PARAMETER[""scale_factor"",1],
    PARAMETER[""false_easting"",0],
    PARAMETER[""false_northing"",0],
    UNIT[""metre"",1,
        AUTHORITY[""EPSG"",""9001""]],
    EXTENSION[""PROJ4"",""+proj=merc +a=6378137 +b=6378137 +lat_ts=0.0 +lon_0=0.0 +x_0=0.0 +y_0=0 +k=1.0 +units=m +nadgrids=@null +wktext +no_defs""],
    AUTHORITY[""EPSG"",""3857""]]"
        });
        
        // 한국 측지계
        Register(new EPSGEntry
        {
            SRID = 5181,
            Name = "Korea 2000 / Unified CS",
            Description = "Korean Unified Coordinate System",
            Type = CoordinateSystemType.Projected,
            AreaOfUse = "South Korea",
            WKT = @"PROJCS[""Korea 2000 / Unified CS"",
    GEOGCS[""Korea 2000"",
        DATUM[""Korea_2000"",
            SPHEROID[""GRS 1980"",6378137,298.257222101,
                AUTHORITY[""EPSG"",""7019""]],
            AUTHORITY[""EPSG"",""6737""]],
        PRIMEM[""Greenwich"",0,
            AUTHORITY[""EPSG"",""8901""]],
        UNIT[""degree"",0.0174532925199433,
            AUTHORITY[""EPSG"",""9122""]],
        AUTHORITY[""EPSG"",""4737""]],
    PROJECTION[""Transverse_Mercator""],
    PARAMETER[""latitude_of_origin"",38],
    PARAMETER[""central_meridian"",127.5],
    PARAMETER[""scale_factor"",0.9996],
    PARAMETER[""false_easting"",1000000],
    PARAMETER[""false_northing"",2000000],
    UNIT[""metre"",1,
        AUTHORITY[""EPSG"",""9001""]],
    AUTHORITY[""EPSG"",""5181""]]"
        });
        
        // 한국 중부원점
        Register(new EPSGEntry
        {
            SRID = 5186,
            Name = "Korea 2000 / Central Belt",
            Description = "Korean Central Belt coordinate system",
            Type = CoordinateSystemType.Projected,
            AreaOfUse = "South Korea - central belt",
            WKT = @"PROJCS[""Korea 2000 / Central Belt"",
    GEOGCS[""Korea 2000"",
        DATUM[""Korea_2000"",
            SPHEROID[""GRS 1980"",6378137,298.257222101,
                AUTHORITY[""EPSG"",""7019""]],
            AUTHORITY[""EPSG"",""6737""]],
        PRIMEM[""Greenwich"",0,
            AUTHORITY[""EPSG"",""8901""]],
        UNIT[""degree"",0.0174532925199433,
            AUTHORITY[""EPSG"",""9122""]],
        AUTHORITY[""EPSG"",""4737""]],
    PROJECTION[""Transverse_Mercator""],
    PARAMETER[""latitude_of_origin"",38],
    PARAMETER[""central_meridian"",127],
    PARAMETER[""scale_factor"",1],
    PARAMETER[""false_easting"",200000],
    PARAMETER[""false_northing"",500000],
    UNIT[""metre"",1,
        AUTHORITY[""EPSG"",""9001""]],
    AUTHORITY[""EPSG"",""5186""]]"
        });
        
        // UTM Zone 52N (한국 지역)
        Register(new EPSGEntry
        {
            SRID = 32652,
            Name = "WGS 84 / UTM zone 52N",
            Description = "WGS84 UTM Zone 52 North",
            Type = CoordinateSystemType.Projected,
            AreaOfUse = "Korea, Japan",
            WKT = @"PROJCS[""WGS 84 / UTM zone 52N"",
    GEOGCS[""WGS 84"",
        DATUM[""WGS_1984"",
            SPHEROID[""WGS 84"",6378137,298.257223563,
                AUTHORITY[""EPSG"",""7030""]],
            AUTHORITY[""EPSG"",""6326""]],
        PRIMEM[""Greenwich"",0,
            AUTHORITY[""EPSG"",""8901""]],
        UNIT[""degree"",0.0174532925199433,
            AUTHORITY[""EPSG"",""9122""]],
        AUTHORITY[""EPSG"",""4326""]],
    PROJECTION[""Transverse_Mercator""],
    PARAMETER[""latitude_of_origin"",0],
    PARAMETER[""central_meridian"",129],
    PARAMETER[""scale_factor"",0.9996],
    PARAMETER[""false_easting"",500000],
    PARAMETER[""false_northing"",0],
    UNIT[""metre"",1,
        AUTHORITY[""EPSG"",""9001""]],
    AUTHORITY[""EPSG"",""32652""]]"
        });
        
        // 구글 지도용 (3857과 동일하지만 다른 이름)
        Register(new EPSGEntry
        {
            SRID = 900913,
            Name = "Google Maps Global Mercator",
            Description = "Spherical Mercator projection used by Google Maps",
            Type = CoordinateSystemType.Projected,
            AreaOfUse = "World",
            WKT = @"PROJCS[""Google Maps Global Mercator"",
    GEOGCS[""WGS 84"",
        DATUM[""WGS_1984"",
            SPHEROID[""WGS 84"",6378137,298.257223563]],
        PRIMEM[""Greenwich"",0],
        UNIT[""degree"",0.0174532925199433]],
    PROJECTION[""Mercator_1SP""],
    PARAMETER[""central_meridian"",0],
    PARAMETER[""scale_factor"",1],
    PARAMETER[""false_easting"",0],
    PARAMETER[""false_northing"",0],
    UNIT[""metre"",1]]"
        });
        
        // 일본 평면직각좌표계 (JGD2011)
        Register(new EPSGEntry
        {
            SRID = 6677,
            Name = "JGD2011 / Japan Plane Rectangular CS IX",
            Description = "Japanese Geodetic Datum 2011 Plane Rectangular CS IX",
            Type = CoordinateSystemType.Projected,
            AreaOfUse = "Japan",
            WKT = @"PROJCS[""JGD2011 / Japan Plane Rectangular CS IX"",
    GEOGCS[""JGD2011"",
        DATUM[""Japanese_Geodetic_Datum_2011"",
            SPHEROID[""GRS 1980"",6378137,298.257222101,
                AUTHORITY[""EPSG"",""7019""]],
            AUTHORITY[""EPSG"",""1128""]],
        PRIMEM[""Greenwich"",0,
            AUTHORITY[""EPSG"",""8901""]],
        UNIT[""degree"",0.0174532925199433,
            AUTHORITY[""EPSG"",""9122""]],
        AUTHORITY[""EPSG"",""6668""]],
    PROJECTION[""Transverse_Mercator""],
    PARAMETER[""latitude_of_origin"",36],
    PARAMETER[""central_meridian"",139.833333333333],
    PARAMETER[""scale_factor"",0.9999],
    PARAMETER[""false_easting"",0],
    PARAMETER[""false_northing"",0],
    UNIT[""metre"",1,
        AUTHORITY[""EPSG"",""9001""]],
    AUTHORITY[""EPSG"",""6677""]]"
        });
    }
}

/// <summary>
/// EPSG 좌표계 항목
/// </summary>
public class EPSGEntry
{
    /// <summary>
    /// EPSG 코드
    /// </summary>
    public int SRID { get; set; }
    
    /// <summary>
    /// 좌표계 이름
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 좌표계 설명
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// 좌표계 타입
    /// </summary>
    public CoordinateSystemType Type { get; set; }
    
    /// <summary>
    /// 사용 지역
    /// </summary>
    public string AreaOfUse { get; set; } = string.Empty;
    
    /// <summary>
    /// WKT (Well-Known Text) 정의
    /// </summary>
    public string WKT { get; set; } = string.Empty;
    
    /// <summary>
    /// Proj4 문자열 (옵션)
    /// </summary>
    public string? Proj4 { get; set; }
    
    /// <summary>
    /// 좌표계의 경계 범위
    /// </summary>
    public Geometry.Envelope? Bounds { get; set; }
    
    /// <summary>
    /// 정확도 (미터 단위)
    /// </summary>
    public double? Accuracy { get; set; }
    
    /// <summary>
    /// 더 이상 사용되지 않는 좌표계 여부
    /// </summary>
    public bool IsDeprecated { get; set; }
    
    /// <summary>
    /// 좌표계 버전
    /// </summary>
    public string? Version { get; set; }
    
    /// <summary>
    /// 추가 메타데이터
    /// </summary>
    public Dictionary<string, string> Metadata { get; } = new();
    
    /// <inheritdoc/>
    public override string ToString()
    {
        return $"EPSG:{SRID} - {Name}";
    }
    
    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is EPSGEntry other && SRID == other.SRID;
    }
    
    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return SRID.GetHashCode();
    }
}

/// <summary>
/// EPSG 데이터베이스 로더
/// 외부 파일이나 데이터베이스에서 EPSG 정의를 로드
/// </summary>
public class EPSGDatabaseLoader
{
    /// <summary>
    /// CSV 파일에서 EPSG 정의 로드
    /// </summary>
    /// <param name="csvFilePath">CSV 파일 경로</param>
    /// <returns>로드된 좌표계 목록</returns>
    public static async Task<IEnumerable<EPSGEntry>> LoadFromCsvAsync(string csvFilePath)
    {
        if (!File.Exists(csvFilePath))
            throw new FileNotFoundException($"EPSG CSV file not found: {csvFilePath}");
        
        var entries = new List<EPSGEntry>();
        
        try
        {
            var lines = await File.ReadAllLinesAsync(csvFilePath);
            
            foreach (var line in lines.Skip(1)) // 헤더 스킵
            {
                var parts = line.Split(',');
                if (parts.Length >= 5)
                {
                    if (int.TryParse(parts[0], out var srid) &&
                        Enum.TryParse<CoordinateSystemType>(parts[3], out var type))
                    {
                        var entry = new EPSGEntry
                        {
                            SRID = srid,
                            Name = parts[1].Trim('"'),
                            Description = parts[2].Trim('"'),
                            Type = type,
                            AreaOfUse = parts[4].Trim('"'),
                            WKT = parts.Length > 5 ? parts[5].Trim('"') : string.Empty
                        };
                        
                        entries.Add(entry);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load EPSG data from CSV: {ex.Message}", ex);
        }
        
        return entries;
    }
    
    /// <summary>
    /// JSON 파일에서 EPSG 정의 로드
    /// </summary>
    /// <param name="jsonFilePath">JSON 파일 경로</param>
    /// <returns>로드된 좌표계 목록</returns>
    public static async Task<IEnumerable<EPSGEntry>> LoadFromJsonAsync(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath))
            throw new FileNotFoundException($"EPSG JSON file not found: {jsonFilePath}");
        
        try
        {
            var jsonContent = await File.ReadAllTextAsync(jsonFilePath);
            var entries = System.Text.Json.JsonSerializer.Deserialize<List<EPSGEntry>>(jsonContent);
            
            return entries ?? Enumerable.Empty<EPSGEntry>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load EPSG data from JSON: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// 모든 로드된 좌표계를 데이터베이스에 등록
    /// </summary>
    /// <param name="entries">좌표계 목록</param>
    /// <returns>등록 성공한 좌표계 수</returns>
    public static int RegisterAll(IEnumerable<EPSGEntry> entries)
    {
        var registeredCount = 0;
        
        foreach (var entry in entries)
        {
            if (EPSGDatabase.Instance.Register(entry))
            {
                registeredCount++;
            }
        }
        
        return registeredCount;
    }
}

/// <summary>
/// 좌표계 타입
/// </summary>
public enum CoordinateSystemType
{
    /// <summary>
    /// 지리 좌표계 (위도/경도)
    /// </summary>
    Geographic,
    
    /// <summary>
    /// 투영 좌표계
    /// </summary>
    Projected,
    
    /// <summary>
    /// 지심 좌표계
    /// </summary>
    Geocentric,
    
    /// <summary>
    /// 수직 좌표계
    /// </summary>
    Vertical,
    
    /// <summary>
    /// 복합 좌표계
    /// </summary>
    Compound,
    
    /// <summary>
    /// 엔지니어링 좌표계
    /// </summary>
    Engineering,
    
    /// <summary>
    /// 기타/알 수 없음
    /// </summary>
    Unknown
}