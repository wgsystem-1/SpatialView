namespace SpatialView.Engine.Geometry;

/// <summary>
/// 최소 경계 사각형 (Minimum Bounding Rectangle)
/// </summary>
public class Envelope
{
    /// <summary>
    /// 최소 X 좌표
    /// </summary>
    public double MinX { get; set; }
    
    /// <summary>
    /// 최대 X 좌표
    /// </summary>
    public double MaxX { get; set; }
    
    /// <summary>
    /// 최소 Y 좌표
    /// </summary>
    public double MinY { get; set; }
    
    /// <summary>
    /// 최대 Y 좌표
    /// </summary>
    public double MaxY { get; set; }
    
    /// <summary>
    /// 중심점 X 좌표
    /// </summary>
    public double CenterX => (MinX + MaxX) / 2.0;
    
    /// <summary>
    /// 중심점 Y 좌표
    /// </summary>
    public double CenterY => (MinY + MaxY) / 2.0;
    
    /// <summary>
    /// 기본 생성자 (비어있는 Envelope)
    /// </summary>
    public Envelope()
    {
        SetToNull();
    }
    
    /// <summary>
    /// 좌표로부터 생성
    /// </summary>
    public Envelope(double x1, double x2, double y1, double y2)
    {
        Init(x1, x2, y1, y2);
    }
    
    /// <summary>
    /// 두 좌표로부터 생성
    /// </summary>
    public Envelope(ICoordinate p1, ICoordinate p2)
    {
        Init(p1, p2);
    }
    
    /// <summary>
    /// 복사 생성자
    /// </summary>
    public Envelope(Envelope env)
    {
        Init(env);
    }
    
    /// <summary>
    /// 이 Envelope이 비어있는지 확인
    /// </summary>
    public bool IsNull => MinX > MaxX;
    
    /// <summary>
    /// 너비
    /// </summary>
    public double Width => IsNull ? 0 : MaxX - MinX;
    
    /// <summary>
    /// 높이
    /// </summary>
    public double Height => IsNull ? 0 : MaxY - MinY;
    
    /// <summary>
    /// 면적
    /// </summary>
    public double Area => Width * Height;
    
    /// <summary>
    /// 중심점
    /// </summary>
    public Coordinate Centre => IsNull 
        ? new Coordinate() 
        : new Coordinate((MinX + MaxX) / 2.0, (MinY + MaxY) / 2.0);
    
    private void SetToNull()
    {
        MinX = 0;
        MaxX = -1;
        MinY = 0;
        MaxY = -1;
    }
    
    private void Init(double x1, double x2, double y1, double y2)
    {
        MinX = x1 < x2 ? x1 : x2;
        MaxX = x1 > x2 ? x1 : x2;
        MinY = y1 < y2 ? y1 : y2;
        MaxY = y1 > y2 ? y1 : y2;
    }
    
    private void Init(ICoordinate p1, ICoordinate p2)
    {
        Init(p1.X, p2.X, p1.Y, p2.Y);
    }
    
    public void Init(Envelope env)
    {
        MinX = env.MinX;
        MaxX = env.MaxX;
        MinY = env.MinY;
        MaxY = env.MaxY;
    }
    
    /// <summary>
    /// 좌표를 포함하도록 확장
    /// </summary>
    public void ExpandToInclude(ICoordinate p)
    {
        ExpandToInclude(p.X, p.Y);
    }
    
    /// <summary>
    /// X, Y 좌표를 포함하도록 확장
    /// </summary>
    public void ExpandToInclude(double x, double y)
    {
        if (IsNull)
        {
            MinX = x;
            MaxX = x;
            MinY = y;
            MaxY = y;
        }
        else
        {
            if (x < MinX) MinX = x;
            if (x > MaxX) MaxX = x;
            if (y < MinY) MinY = y;
            if (y > MaxY) MaxY = y;
        }
    }
    
    /// <summary>
    /// 다른 Envelope를 포함하도록 확장
    /// </summary>
    public void ExpandToInclude(Envelope other)
    {
        if (other.IsNull) return;
        if (IsNull)
        {
            MinX = other.MinX;
            MaxX = other.MaxX;
            MinY = other.MinY;
            MaxY = other.MaxY;
        }
        else
        {
            if (other.MinX < MinX) MinX = other.MinX;
            if (other.MaxX > MaxX) MaxX = other.MaxX;
            if (other.MinY < MinY) MinY = other.MinY;
            if (other.MaxY > MaxY) MaxY = other.MaxY;
        }
    }
    
    /// <summary>
    /// 좌표가 포함되어 있는지 확인
    /// </summary>
    public bool Contains(ICoordinate p)
    {
        return Contains(p.X, p.Y);
    }
    
    /// <summary>
    /// X, Y 좌표가 포함되어 있는지 확인
    /// </summary>
    public bool Contains(double x, double y)
    {
        return !IsNull && x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }
    
    /// <summary>
    /// 다른 Envelope가 포함되어 있는지 확인
    /// </summary>
    public bool Contains(Envelope other)
    {
        return !IsNull && !other.IsNull &&
               other.MinX >= MinX && other.MaxX <= MaxX &&
               other.MinY >= MinY && other.MaxY <= MaxY;
    }
    
    /// <summary>
    /// 다른 Envelope와 교차하는지 확인
    /// </summary>
    public bool Intersects(Envelope other)
    {
        if (IsNull || other.IsNull) return false;
        return !(other.MinX > MaxX || other.MaxX < MinX || 
                 other.MinY > MaxY || other.MaxY < MinY);
    }
    
    /// <summary>
    /// 두 Envelope의 합집합 반환
    /// </summary>
    public Envelope Union(Envelope other)
    {
        if (IsNull) return other.Copy();
        if (other.IsNull) return Copy();
        
        return new Envelope(
            Math.Min(MinX, other.MinX),
            Math.Max(MaxX, other.MaxX),
            Math.Min(MinY, other.MinY),
            Math.Max(MaxY, other.MaxY));
    }
    
    /// <summary>
    /// 두 Envelope의 교집합 반환
    /// </summary>
    public Envelope? Intersection(Envelope other)
    {
        if (!Intersects(other)) return null;
        
        return new Envelope(
            Math.Max(MinX, other.MinX),
            Math.Min(MaxX, other.MaxX),
            Math.Max(MinY, other.MinY),
            Math.Min(MaxY, other.MaxY));
    }
    
    /// <summary>
    /// 다른 Envelope를 포함하기 위한 확장량 계산
    /// </summary>
    public double GetEnlargement(Envelope other)
    {
        if (other.IsNull) return 0;
        
        var unionEnv = Union(other);
        return unionEnv.Area - Area;
    }
    
    /// <summary>
    /// 복사본 생성
    /// </summary>
    public Envelope Copy()
    {
        return new Envelope(this);
    }
    
    /// <summary>
    /// 지정된 거리만큼 확장
    /// </summary>
    public void ExpandBy(double distance)
    {
        if (IsNull) return;
        MinX -= distance;
        MaxX += distance;
        MinY -= distance;
        MaxY += distance;
    }
    
    /// <summary>
    /// 지정된 거리만큼 확장 (X, Y 방향 각각)
    /// </summary>
    public void ExpandBy(double deltaX, double deltaY)
    {
        if (IsNull) return;
        MinX -= deltaX;
        MaxX += deltaX;
        MinY -= deltaY;
        MaxY += deltaY;
    }
    
    /// <summary>
    /// Envelope을 Polygon으로 변환
    /// </summary>
    /// <returns>사각형 Polygon</returns>
    public Polygon ToPolygon()
    {
        if (IsNull) return Polygon.Empty;
        
        var coords = new List<ICoordinate>
        {
            new Coordinate(MinX, MinY),
            new Coordinate(MaxX, MinY),
            new Coordinate(MaxX, MaxY),
            new Coordinate(MinX, MaxY),
            new Coordinate(MinX, MinY)  // 폐곡선
        };
        
        var ring = new LinearRing(coords);
        return new Polygon(ring);
    }
    
    public override string ToString()
    {
        return $"Envelope[{MinX:F6}, {MaxX:F6}, {MinY:F6}, {MaxY:F6}]";
    }
}