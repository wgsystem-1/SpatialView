using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Analysis.Operations;

/// <summary>
/// Buffer 연산을 수행하는 클래스
/// 지오메트리 주변에 지정된 거리만큼의 버퍼 영역을 생성
/// </summary>
public class BufferOperation
{
    private readonly BufferParameters _parameters;
    
    public BufferOperation(BufferParameters? parameters = null)
    {
        _parameters = parameters ?? BufferParameters.Default;
    }
    
    /// <summary>
    /// 지오메트리에 대한 버퍼 생성
    /// </summary>
    public IGeometry Buffer(IGeometry geometry, double distance)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));
            
        if (distance == 0)
            return geometry;
            
        return geometry.GeometryType switch
        {
            GeometryType.Point => BufferPoint((Point)geometry, distance),
            GeometryType.LineString => BufferLineString((LineString)geometry, distance),
            GeometryType.Polygon => BufferPolygon((Polygon)geometry, distance),
            GeometryType.MultiPoint => BufferMultiGeometry((MultiPoint)geometry, distance),
            GeometryType.MultiLineString => BufferMultiGeometry((MultiLineString)geometry, distance),
            GeometryType.MultiPolygon => BufferMultiGeometry((MultiPolygon)geometry, distance),
            GeometryType.GeometryCollection => BufferGeometryCollection((GeometryCollection)geometry, distance),
            _ => throw new NotSupportedException($"Buffer operation not supported for {geometry.GeometryType}")
        };
    }
    
    /// <summary>
    /// Point에 대한 버퍼 생성 (원)
    /// </summary>
    private Polygon BufferPoint(Point point, double distance)
    {
        var segments = _parameters.QuadrantSegments * 4;
        var coordinates = new List<ICoordinate>();
        
        for (int i = 0; i <= segments; i++)
        {
            var angle = (2 * Math.PI * i) / segments;
            var x = point.X + distance * Math.Cos(angle);
            var y = point.Y + distance * Math.Sin(angle);
            
            coordinates.Add(new Coordinate(x, y));
        }
        
        return new Polygon(new LinearRing(coordinates.ToArray()));
    }
    
    /// <summary>
    /// LineString에 대한 버퍼 생성
    /// </summary>
    private Polygon BufferLineString(LineString lineString, double distance)
    {
        var coordinates = lineString.Coordinates;
        if (coordinates.Length < 2)
            return new Polygon(new LinearRing(new ICoordinate[] 
            { 
                new Coordinate(0, 0), 
                new Coordinate(0, 0), 
                new Coordinate(0, 0), 
                new Coordinate(0, 0) 
            }));
        
        var leftOffsets = new List<ICoordinate>();
        var rightOffsets = new List<ICoordinate>();
        
        // 각 세그먼트에 대해 오프셋 계산
        for (int i = 0; i < coordinates.Length - 1; i++)
        {
            var p1 = coordinates[i];
            var p2 = coordinates[i + 1];
            
            // 세그먼트의 방향 벡터
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            
            if (length == 0) continue;
            
            // 정규화된 수직 벡터
            var nx = -dy / length;
            var ny = dx / length;
            
            // 왼쪽 오프셋
            leftOffsets.Add(new Coordinate(p1.X + nx * distance, p1.Y + ny * distance));
            if (i == coordinates.Length - 2)
                leftOffsets.Add(new Coordinate(p2.X + nx * distance, p2.Y + ny * distance));
            
            // 오른쪽 오프셋
            rightOffsets.Insert(0, new Coordinate(p1.X - nx * distance, p1.Y - ny * distance));
            if (i == coordinates.Length - 2)
                rightOffsets.Insert(0, new Coordinate(p2.X - nx * distance, p2.Y - ny * distance));
        }
        
        // 끝점에 반원 추가
        AddEndCap(leftOffsets, coordinates[coordinates.Length - 1], coordinates[coordinates.Length - 2], distance, false);
        AddEndCap(rightOffsets, coordinates[0], coordinates[1], distance, true);
        
        // 결합
        var bufferCoords = new List<ICoordinate>();
        bufferCoords.AddRange(leftOffsets);
        bufferCoords.AddRange(rightOffsets);
        bufferCoords.Add(leftOffsets[0]); // 폐합
        
        return new Polygon(new LinearRing(bufferCoords.ToArray()));
    }
    
    /// <summary>
    /// 끝점에 반원 캡 추가
    /// </summary>
    private void AddEndCap(List<ICoordinate> coords, ICoordinate center, ICoordinate reference, double distance, bool reverse)
    {
        if (_parameters.EndCapStyle != EndCapStyle.Round) return;
        
        var dx = center.X - reference.X;
        var dy = center.Y - reference.Y;
        var startAngle = Math.Atan2(dy, dx);
        
        var segments = _parameters.QuadrantSegments * 2;
        var angleIncrement = Math.PI / segments;
        
        var capCoords = new List<ICoordinate>();
        for (int i = 1; i < segments; i++)
        {
            var angle = startAngle + (reverse ? -angleIncrement * i : angleIncrement * i);
            var x = center.X + distance * Math.Cos(angle);
            var y = center.Y + distance * Math.Sin(angle);
            capCoords.Add(new Coordinate(x, y));
        }
        
        if (reverse) capCoords.Reverse();
        coords.AddRange(capCoords);
    }
    
    /// <summary>
    /// Polygon에 대한 버퍼 생성
    /// </summary>
    private Polygon BufferPolygon(Polygon polygon, double distance)
    {
        // 양의 거리: 확장, 음의 거리: 축소
        if (distance > 0)
        {
            return ExpandPolygon(polygon, distance);
        }
        else
        {
            return ShrinkPolygon(polygon, -distance);
        }
    }
    
    /// <summary>
    /// 폴리곤 확장
    /// </summary>
    private Polygon ExpandPolygon(Polygon polygon, double distance)
    {
        var exterior = polygon.ExteriorRing;
        var expandedExterior = OffsetRing(exterior, distance, true);
        
        var holes = new List<LinearRing>();
        foreach (var hole in polygon.InteriorRings)
        {
            var shrunkHole = OffsetRing(hole, -distance, false);
            if (shrunkHole != null && shrunkHole.Coordinates.Length >= 4)
            {
                holes.Add(shrunkHole);
            }
        }
        
        return new Polygon(expandedExterior, holes.ToArray());
    }
    
    /// <summary>
    /// 폴리곤 축소
    /// </summary>
    private Polygon ShrinkPolygon(Polygon polygon, double distance)
    {
        var exterior = polygon.ExteriorRing;
        var shrunkExterior = OffsetRing(exterior, -distance, true);
        
        if (shrunkExterior == null || shrunkExterior.Coordinates.Length < 4)
        {
            // 너무 작아서 사라짐
            return new Polygon(new LinearRing(new ICoordinate[] 
            { 
                new Coordinate(0, 0), 
                new Coordinate(0, 0), 
                new Coordinate(0, 0), 
                new Coordinate(0, 0) 
            }));
        }
        
        var holes = new List<LinearRing>();
        foreach (var hole in polygon.InteriorRings)
        {
            var expandedHole = OffsetRing(hole, distance, false);
            if (expandedHole != null && expandedHole.Coordinates.Length >= 4)
            {
                holes.Add(expandedHole);
            }
        }
        
        return new Polygon(shrunkExterior, holes.ToArray());
    }
    
    /// <summary>
    /// 링 오프셋
    /// </summary>
    private LinearRing? OffsetRing(LinearRing ring, double distance, bool isExterior)
    {
        var coords = ring.Coordinates;
        if (coords.Length < 4) return null;
        
        var offsetCoords = new List<ICoordinate>();
        
        // 각 정점에서 오프셋 계산
        for (int i = 0; i < coords.Length - 1; i++)
        {
            var prev = i == 0 ? coords[coords.Length - 2] : coords[i - 1];
            var curr = coords[i];
            var next = coords[i + 1];
            
            var offset = CalculateVertexOffset(prev, curr, next, distance, isExterior);
            if (offset != null)
                offsetCoords.Add(offset);
        }
        
        if (offsetCoords.Count < 3) return null;
        
        // 폐합
        offsetCoords.Add(offsetCoords[0]);
        
        return new LinearRing(offsetCoords.ToArray());
    }
    
    /// <summary>
    /// 정점에서의 오프셋 계산
    /// </summary>
    private ICoordinate? CalculateVertexOffset(ICoordinate prev, ICoordinate curr, ICoordinate next, double distance, bool isExterior)
    {
        // 들어오는 벡터
        var dx1 = curr.X - prev.X;
        var dy1 = curr.Y - prev.Y;
        var len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
        
        // 나가는 벡터
        var dx2 = next.X - curr.X;
        var dy2 = next.Y - curr.Y;
        var len2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
        
        if (len1 == 0 || len2 == 0) return null;
        
        // 정규화
        dx1 /= len1; dy1 /= len1;
        dx2 /= len2; dy2 /= len2;
        
        // 수직 벡터 (오른쪽 방향)
        var nx1 = -dy1;
        var ny1 = dx1;
        var nx2 = -dy2;
        var ny2 = dx2;
        
        // 외곽선이 아니면 방향 반대
        if (!isExterior)
        {
            nx1 = -nx1; ny1 = -ny1;
            nx2 = -nx2; ny2 = -ny2;
        }
        
        // 이등분선 방향
        var bisectorX = nx1 + nx2;
        var bisectorY = ny1 + ny2;
        var bisectorLen = Math.Sqrt(bisectorX * bisectorX + bisectorY * bisectorY);
        
        if (bisectorLen == 0)
        {
            // 180도 회전 - 수직 방향 사용
            return new Coordinate(curr.X + nx1 * distance, curr.Y + ny1 * distance);
        }
        
        // 오프셋 거리 계산
        var sinHalfAngle = Math.Sqrt((1 - (dx1 * dx2 + dy1 * dy2)) / 2);
        var offsetDistance = Math.Abs(distance / sinHalfAngle);
        
        // 최대 거리 제한
        if (offsetDistance > Math.Abs(distance) * _parameters.MitreLimit)
        {
            offsetDistance = Math.Abs(distance) * _parameters.MitreLimit;
        }
        
        bisectorX /= bisectorLen;
        bisectorY /= bisectorLen;
        
        return new Coordinate(
            curr.X + bisectorX * offsetDistance,
            curr.Y + bisectorY * offsetDistance
        );
    }
    
    /// <summary>
    /// MultiGeometry에 대한 버퍼
    /// </summary>
    private IGeometry BufferMultiGeometry(IGeometry multiGeometry, double distance)
    {
        var bufferedGeometries = new List<Polygon>();
        
        if (multiGeometry is MultiPoint mp)
        {
            foreach (var point in mp.Geometries.Cast<Point>())
            {
                bufferedGeometries.Add(BufferPoint(point, distance));
            }
        }
        else if (multiGeometry is MultiLineString mls)
        {
            foreach (var line in mls.Geometries.Cast<LineString>())
            {
                bufferedGeometries.Add(BufferLineString(line, distance));
            }
        }
        else if (multiGeometry is MultiPolygon mpoly)
        {
            foreach (var poly in mpoly.Geometries.Cast<Polygon>())
            {
                bufferedGeometries.Add(BufferPolygon(poly, distance));
            }
        }
        
        // 버퍼된 폴리곤들을 Union
        if (bufferedGeometries.Count == 0)
            return new MultiPolygon();
        if (bufferedGeometries.Count == 1)
            return bufferedGeometries[0];
            
        // 간단한 Union - 실제로는 더 복잡한 알고리즘 필요
        return new MultiPolygon(bufferedGeometries.ToArray());
    }
    
    /// <summary>
    /// GeometryCollection에 대한 버퍼
    /// </summary>
    private IGeometry BufferGeometryCollection(GeometryCollection collection, double distance)
    {
        var bufferedGeometries = new List<IGeometry>();
        
        foreach (var geom in collection.Geometries)
        {
            var buffered = Buffer(geom, distance);
            bufferedGeometries.Add(buffered);
        }
        
        return new GeometryCollection(bufferedGeometries.ToArray());
    }
}

/// <summary>
/// 버퍼 연산 매개변수
/// </summary>
public class BufferParameters
{
    /// <summary>
    /// 사분원당 세그먼트 수 (원형 근사)
    /// </summary>
    public int QuadrantSegments { get; set; } = 8;
    
    /// <summary>
    /// 끝 캡 스타일
    /// </summary>
    public EndCapStyle EndCapStyle { get; set; } = EndCapStyle.Round;
    
    /// <summary>
    /// 조인 스타일
    /// </summary>
    public JoinStyle JoinStyle { get; set; } = JoinStyle.Round;
    
    /// <summary>
    /// Mitre join의 최대 비율
    /// </summary>
    public double MitreLimit { get; set; } = 5.0;
    
    /// <summary>
    /// 단일 면 버퍼 여부
    /// </summary>
    public bool IsSingleSided { get; set; } = false;
    
    /// <summary>
    /// 기본 매개변수
    /// </summary>
    public static BufferParameters Default => new BufferParameters();
    
    /// <summary>
    /// 빠른 연산을 위한 매개변수
    /// </summary>
    public static BufferParameters Fast => new BufferParameters 
    { 
        QuadrantSegments = 4,
        MitreLimit = 2.0
    };
    
    /// <summary>
    /// 고품질 연산을 위한 매개변수
    /// </summary>
    public static BufferParameters HighQuality => new BufferParameters 
    { 
        QuadrantSegments = 16,
        MitreLimit = 10.0
    };
}

/// <summary>
/// 끝 캡 스타일
/// </summary>
public enum EndCapStyle
{
    /// <summary>
    /// 둥근 끝
    /// </summary>
    Round,
    
    /// <summary>
    /// 평평한 끝
    /// </summary>
    Flat,
    
    /// <summary>
    /// 사각형 끝
    /// </summary>
    Square
}

/// <summary>
/// 조인 스타일
/// </summary>
public enum JoinStyle
{
    /// <summary>
    /// 둥근 조인
    /// </summary>
    Round,
    
    /// <summary>
    /// 마이터 조인
    /// </summary>
    Mitre,
    
    /// <summary>
    /// 베벨 조인
    /// </summary>
    Bevel
}