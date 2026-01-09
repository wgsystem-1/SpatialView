using System.Collections.Concurrent;

namespace SpatialView.Engine.CoordinateSystems.Transformation;

/// <summary>
/// 좌표 변환 엔진
/// 다양한 좌표계 간의 변환을 수행하는 중앙 엔진
/// </summary>
public class CoordinateTransformationEngine
{
    private static readonly Lazy<CoordinateTransformationEngine> _instance = new(() => new CoordinateTransformationEngine());
    
    /// <summary>
    /// 싱글톤 인스턴스
    /// </summary>
    public static CoordinateTransformationEngine Instance => _instance.Value;
    
    private readonly ConcurrentDictionary<string, ICoordinateTransformation> _transformationCache;
    private readonly CoordinateTransformationFactory _transformationFactory;
    private readonly CoordinateSystemFactory _coordinateSystemFactory;
    
    /// <summary>
    /// 캐시 크기 제한
    /// </summary>
    public int CacheLimit { get; set; } = 1000;
    
    /// <summary>
    /// 캐시된 변환 개수
    /// </summary>
    public int CachedTransformationCount => _transformationCache.Count;
    
    /// <summary>
    /// 생성자
    /// </summary>
    private CoordinateTransformationEngine()
    {
        _transformationCache = new ConcurrentDictionary<string, ICoordinateTransformation>();
        _coordinateSystemFactory = new CoordinateSystemFactory();
        _transformationFactory = new CoordinateTransformationFactory();
    }
    
    /// <summary>
    /// EPSG 코드로 좌표 변환 생성
    /// </summary>
    /// <param name="sourceSRID">소스 EPSG 코드</param>
    /// <param name="targetSRID">대상 EPSG 코드</param>
    /// <returns>좌표 변환 객체</returns>
    public ICoordinateTransformation CreateTransformation(int sourceSRID, int targetSRID)
    {
        // 동일한 좌표계인 경우 항등 변환
        if (sourceSRID == targetSRID)
        {
            var coordinateSystem = GetCoordinateSystem(sourceSRID);
            return new IdentityTransformation(coordinateSystem);
        }
        
        // 캐시 키 생성
        var cacheKey = $"{sourceSRID}->{targetSRID}";
        
        // 캐시된 변환 확인
        if (_transformationCache.TryGetValue(cacheKey, out var cachedTransformation))
        {
            return cachedTransformation;
        }
        
        // 좌표계 생성
        var sourceCS = _coordinateSystemFactory.CreateFromSRID(sourceSRID);
        var targetCS = _coordinateSystemFactory.CreateFromSRID(targetSRID);
        
        if (sourceCS == null)
            throw new ArgumentException($"Unknown source coordinate system: EPSG:{sourceSRID}");
        
        if (targetCS == null)
            throw new ArgumentException($"Unknown target coordinate system: EPSG:{targetSRID}");
        
        // 변환 생성
        var transformation = _transformationFactory.CreateTransformation(sourceCS, targetCS);
        
        // 캐시에 저장 (크기 제한 확인)
        if (_transformationCache.Count < CacheLimit)
        {
            _transformationCache.TryAdd(cacheKey, transformation);
        }
        else
        {
            // 캐시가 가득 찬 경우 일부 제거 후 추가
            ClearOldestTransformations();
            _transformationCache.TryAdd(cacheKey, transformation);
        }
        
        return transformation;
    }
    
    /// <summary>
    /// 좌표계 객체로 좌표 변환 생성
    /// </summary>
    /// <param name="sourceCS">소스 좌표계</param>
    /// <param name="targetCS">대상 좌표계</param>
    /// <returns>좌표 변환 객체</returns>
    public ICoordinateTransformation CreateTransformation(ICoordinateSystem sourceCS, ICoordinateSystem targetCS)
    {
        if (sourceCS == null)
            throw new ArgumentNullException(nameof(sourceCS));
        
        if (targetCS == null)
            throw new ArgumentNullException(nameof(targetCS));
        
        // 동일한 좌표계인 경우
        if (sourceCS.AuthorityCode == targetCS.AuthorityCode && 
            string.Equals(sourceCS.Authority, targetCS.Authority, StringComparison.OrdinalIgnoreCase))
        {
            return new IdentityTransformation(sourceCS);
        }
        
        return _transformationFactory.CreateTransformation(sourceCS, targetCS);
    }
    
    /// <summary>
    /// WKT 문자열로 좌표 변환 생성
    /// </summary>
    /// <param name="sourceWKT">소스 좌표계 WKT</param>
    /// <param name="targetWKT">대상 좌표계 WKT</param>
    /// <returns>좌표 변환 객체</returns>
    public ICoordinateTransformation CreateTransformation(string sourceWKT, string targetWKT)
    {
        if (string.IsNullOrWhiteSpace(sourceWKT))
            throw new ArgumentException("Source WKT cannot be null or empty", nameof(sourceWKT));
        
        if (string.IsNullOrWhiteSpace(targetWKT))
            throw new ArgumentException("Target WKT cannot be null or empty", nameof(targetWKT));
        
        var sourceCS = _coordinateSystemFactory.CreateFromWKT(sourceWKT);
        var targetCS = _coordinateSystemFactory.CreateFromWKT(targetWKT);
        
        return CreateTransformation(sourceCS, targetCS);
    }
    
    /// <summary>
    /// 단일 점 변환
    /// </summary>
    /// <param name="point">변환할 점</param>
    /// <param name="sourceSRID">소스 EPSG 코드</param>
    /// <param name="targetSRID">대상 EPSG 코드</param>
    /// <returns>변환된 점</returns>
    public Geometry.ICoordinate Transform(Geometry.ICoordinate point, int sourceSRID, int targetSRID)
    {
        var transformation = CreateTransformation(sourceSRID, targetSRID);
        return transformation.Transform(point);
    }
    
    /// <summary>
    /// 점 배열 변환
    /// </summary>
    /// <param name="points">변환할 점들</param>
    /// <param name="sourceSRID">소스 EPSG 코드</param>
    /// <param name="targetSRID">대상 EPSG 코드</param>
    /// <returns>변환된 점들</returns>
    public Geometry.ICoordinate[] Transform(Geometry.ICoordinate[] points, int sourceSRID, int targetSRID)
    {
        var transformation = CreateTransformation(sourceSRID, targetSRID);
        return transformation.Transform(points);
    }
    
    /// <summary>
    /// 지오메트리 변환
    /// </summary>
    /// <param name="geometry">변환할 지오메트리</param>
    /// <param name="sourceSRID">소스 EPSG 코드</param>
    /// <param name="targetSRID">대상 EPSG 코드</param>
    /// <returns>변환된 지오메트리</returns>
    public Geometry.IGeometry Transform(Geometry.IGeometry geometry, int sourceSRID, int targetSRID)
    {
        var transformation = CreateTransformation(sourceSRID, targetSRID);
        return transformation.Transform(geometry);
    }
    
    /// <summary>
    /// 경계 영역 변환
    /// </summary>
    /// <param name="envelope">변환할 경계</param>
    /// <param name="sourceSRID">소스 EPSG 코드</param>
    /// <param name="targetSRID">대상 EPSG 코드</param>
    /// <returns>변환된 경계</returns>
    public Geometry.Envelope TransformEnvelope(Geometry.Envelope envelope, int sourceSRID, int targetSRID)
    {
        if (sourceSRID == targetSRID)
            return envelope.Copy();
        
        // 경계 상자의 모든 모서리 점들을 변환
        var transformation = CreateTransformation(sourceSRID, targetSRID);
        
        var points = new[]
        {
            new Geometry.Coordinate(envelope.MinX, envelope.MinY),
            new Geometry.Coordinate(envelope.MaxX, envelope.MinY),
            new Geometry.Coordinate(envelope.MaxX, envelope.MaxY),
            new Geometry.Coordinate(envelope.MinX, envelope.MaxY),
            new Geometry.Coordinate(envelope.MinX + (envelope.MaxX - envelope.MinX) / 2, envelope.MinY + (envelope.MaxY - envelope.MinY) / 2) // 중심점
        };
        
        var transformedPoints = transformation.Transform(points);
        
        // 변환된 점들로부터 새로운 경계 생성
        var resultEnvelope = new Geometry.Envelope();
        foreach (var point in transformedPoints)
        {
            resultEnvelope.ExpandToInclude(point);
        }
        
        return resultEnvelope;
    }
    
    /// <summary>
    /// 좌표계가 지리 좌표계인지 확인
    /// </summary>
    /// <param name="srid">EPSG 코드</param>
    /// <returns>지리 좌표계 여부</returns>
    public bool IsGeographic(int srid)
    {
        var cs = _coordinateSystemFactory.CreateFromSRID(srid);
        return cs is IGeographicCoordinateSystem;
    }
    
    /// <summary>
    /// 좌표계가 투영 좌표계인지 확인
    /// </summary>
    /// <param name="srid">EPSG 코드</param>
    /// <returns>투영 좌표계 여부</returns>
    public bool IsProjected(int srid)
    {
        var cs = _coordinateSystemFactory.CreateFromSRID(srid);
        return cs is IProjectedCoordinateSystem;
    }
    
    /// <summary>
    /// 두 좌표계가 호환 가능한지 확인
    /// </summary>
    /// <param name="sourceSRID">소스 EPSG 코드</param>
    /// <param name="targetSRID">대상 EPSG 코드</param>
    /// <returns>변환 가능 여부</returns>
    public bool CanTransform(int sourceSRID, int targetSRID)
    {
        try
        {
            CreateTransformation(sourceSRID, targetSRID);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// SRID로부터 좌표계 가져오기
    /// </summary>
    /// <param name="srid">EPSG 코드</param>
    /// <returns>좌표계</returns>
    private ICoordinateSystem GetCoordinateSystem(int srid)
    {
        return _coordinateSystemFactory.CreateFromSRID(srid);
    }
    
    /// <summary>
    /// 변환 정확도 정보 가져오기
    /// </summary>
    /// <param name="sourceSRID">소스 EPSG 코드</param>
    /// <param name="targetSRID">대상 EPSG 코드</param>
    /// <returns>변환 정확도 (미터)</returns>
    public double GetTransformationAccuracy(int sourceSRID, int targetSRID)
    {
        if (sourceSRID == targetSRID)
            return 0.0; // 항등 변환은 완전 정확
        
        try
        {
            var transformation = CreateTransformation(sourceSRID, targetSRID);
            return transformation is MathTransformation mathTransform ? 
                mathTransform.Accuracy : 1.0; // 기본 정확도
        }
        catch
        {
            return double.MaxValue; // 변환 불가능
        }
    }
    
    /// <summary>
    /// 캐시 지우기
    /// </summary>
    public void ClearCache()
    {
        _transformationCache.Clear();
    }
    
    /// <summary>
    /// 오래된 변환들 제거
    /// </summary>
    private void ClearOldestTransformations()
    {
        // 간단한 LRU 구현: 절반 제거
        var entriesToRemove = _transformationCache.Take(_transformationCache.Count / 2).ToList();
        
        foreach (var entry in entriesToRemove)
        {
            _transformationCache.TryRemove(entry.Key, out _);
        }
    }
    
    /// <summary>
    /// 엔진 통계 정보 가져오기
    /// </summary>
    /// <returns>통계 정보</returns>
    public TransformationEngineStatistics GetStatistics()
    {
        return new TransformationEngineStatistics
        {
            CachedTransformations = _transformationCache.Count,
            CacheLimit = CacheLimit,
            SupportedCoordinateSystems = EPSGDatabase.Instance.GetAll().Count()
        };
    }
}

/// <summary>
/// 좌표 변환 엔진 통계 정보
/// </summary>
public class TransformationEngineStatistics
{
    /// <summary>
    /// 캐시된 변환 수
    /// </summary>
    public int CachedTransformations { get; set; }
    
    /// <summary>
    /// 캐시 크기 제한
    /// </summary>
    public int CacheLimit { get; set; }
    
    /// <summary>
    /// 지원되는 좌표계 수
    /// </summary>
    public int SupportedCoordinateSystems { get; set; }
    
    /// <summary>
    /// 캐시 사용률
    /// </summary>
    public double CacheUsageRatio => CacheLimit > 0 ? (double)CachedTransformations / CacheLimit : 0;
    
    /// <inheritdoc/>
    public override string ToString()
    {
        return $"Cached: {CachedTransformations}/{CacheLimit}, Supported CS: {SupportedCoordinateSystems}";
    }
}

/// <summary>
/// 수학적 변환 기본 클래스
/// 실제 좌표 변환 로직을 구현하는 기반 클래스
/// </summary>
public abstract class MathTransformation : ICoordinateTransformation
{
    /// <summary>
    /// 소스 좌표계
    /// </summary>
    public ICoordinateSystem SourceCoordinateSystem { get; }
    
    /// <summary>
    /// 대상 좌표계
    /// </summary>
    public ICoordinateSystem TargetCoordinateSystem { get; }
    
    /// <summary>
    /// 변환 정확도 (미터)
    /// </summary>
    public virtual double Accuracy => 1.0;
    
    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="sourceCS">소스 좌표계</param>
    /// <param name="targetCS">대상 좌표계</param>
    protected MathTransformation(ICoordinateSystem sourceCS, ICoordinateSystem targetCS)
    {
        SourceCoordinateSystem = sourceCS ?? throw new ArgumentNullException(nameof(sourceCS));
        TargetCoordinateSystem = targetCS ?? throw new ArgumentNullException(nameof(targetCS));
    }
    
    /// <summary>
    /// 단일 점 변환 (구현 필요)
    /// </summary>
    /// <param name="sourceCoordinate">소스 좌표</param>
    /// <returns>변환된 좌표</returns>
    public abstract Geometry.ICoordinate Transform(Geometry.ICoordinate sourceCoordinate);
    
    /// <summary>
    /// 좌표 배열 변환
    /// </summary>
    /// <param name="sourceCoordinates">소스 좌표 배열</param>
    /// <returns>변환된 좌표 배열</returns>
    public virtual Geometry.ICoordinate[] Transform(Geometry.ICoordinate[] sourceCoordinates)
    {
        if (sourceCoordinates == null)
            throw new ArgumentNullException(nameof(sourceCoordinates));
        
        var result = new Geometry.ICoordinate[sourceCoordinates.Length];
        
        for (int i = 0; i < sourceCoordinates.Length; i++)
        {
            result[i] = Transform(sourceCoordinates[i]);
        }
        
        return result;
    }
    
    /// <summary>
    /// 지오메트리 변환
    /// </summary>
    /// <param name="geometry">소스 지오메트리</param>
    /// <returns>변환된 지오메트리</returns>
    public virtual Geometry.IGeometry Transform(Geometry.IGeometry geometry)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));
        
        return geometry switch
        {
            Geometry.Point point => TransformPoint(point),
            Geometry.LineString lineString => TransformLineString(lineString),
            Geometry.Polygon polygon => TransformPolygon(polygon),
            Geometry.MultiPoint multiPoint => TransformMultiPoint(multiPoint),
            Geometry.MultiLineString multiLineString => TransformMultiLineString(multiLineString),
            Geometry.MultiPolygon multiPolygon => TransformMultiPolygon(multiPolygon),
            Geometry.GeometryCollection geometryCollection => TransformGeometryCollection(geometryCollection),
            _ => throw new NotSupportedException($"Geometry type {geometry.GetType().Name} is not supported for transformation")
        };
    }
    
    /// <summary>
    /// Point 변환
    /// </summary>
    protected virtual Geometry.Point TransformPoint(Geometry.Point point)
    {
        var transformedCoord = Transform(point.Coordinate);
        return new Geometry.Point(transformedCoord);
    }
    
    /// <summary>
    /// LineString 변환
    /// </summary>
    protected virtual Geometry.LineString TransformLineString(Geometry.LineString lineString)
    {
        var transformedCoords = Transform(lineString.Coordinates);
        return new Geometry.LineString(transformedCoords);
    }
    
    /// <summary>
    /// Polygon 변환
    /// </summary>
    protected virtual Geometry.Polygon TransformPolygon(Geometry.Polygon polygon)
    {
        var transformedExterior = Transform(polygon.ExteriorRing.Coordinates);
        var exterior = new Geometry.LinearRing(transformedExterior);
        
        var interiorRings = new List<Geometry.LinearRing>();
        foreach (var interior in polygon.InteriorRings)
        {
            var transformedInterior = Transform(interior.Coordinates);
            interiorRings.Add(new Geometry.LinearRing(transformedInterior));
        }
        
        return new Geometry.Polygon(exterior, interiorRings.ToArray());
    }
    
    /// <summary>
    /// MultiPoint 변환
    /// </summary>
    protected virtual Geometry.MultiPoint TransformMultiPoint(Geometry.MultiPoint multiPoint)
    {
        var transformedPoints = multiPoint.Geometries
            .Cast<Geometry.Point>()
            .Select(TransformPoint)
            .Cast<Geometry.IGeometry>()
            .ToArray();
        
        return new Geometry.MultiPoint(transformedPoints.Cast<Geometry.Point>().ToArray());
    }
    
    /// <summary>
    /// MultiLineString 변환
    /// </summary>
    protected virtual Geometry.MultiLineString TransformMultiLineString(Geometry.MultiLineString multiLineString)
    {
        var transformedLineStrings = multiLineString.Geometries
            .Cast<Geometry.LineString>()
            .Select(TransformLineString)
            .Cast<Geometry.IGeometry>()
            .ToArray();
        
        return new Geometry.MultiLineString(transformedLineStrings.Cast<Geometry.LineString>().ToArray());
    }
    
    /// <summary>
    /// MultiPolygon 변환
    /// </summary>
    protected virtual Geometry.MultiPolygon TransformMultiPolygon(Geometry.MultiPolygon multiPolygon)
    {
        var transformedPolygons = multiPolygon.Geometries
            .Cast<Geometry.Polygon>()
            .Select(TransformPolygon)
            .Cast<Geometry.IGeometry>()
            .ToArray();
        
        return new Geometry.MultiPolygon(transformedPolygons.Cast<Geometry.Polygon>().ToArray());
    }
    
    /// <summary>
    /// GeometryCollection 변환
    /// </summary>
    protected virtual Geometry.GeometryCollection TransformGeometryCollection(Geometry.GeometryCollection geometryCollection)
    {
        var transformedGeometries = geometryCollection.Geometries
            .Select(Transform)
            .ToArray();
        
        return new Geometry.GeometryCollection(transformedGeometries);
    }
}