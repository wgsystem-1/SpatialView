namespace SpatialView.Engine.Geometry;

/// <summary>
/// 리니어링 - 폐곡선 LineString
/// 폴리곤의 경계를 나타냄
/// </summary>
public class LinearRing : LineString
{
    /// <summary>
    /// 빈 LinearRing 인스턴스
    /// </summary>
    public new static readonly LinearRing Empty = new LinearRing();
    
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public LinearRing() : base()
    {
    }
    
    /// <summary>
    /// 좌표 배열로부터 생성
    /// </summary>
    public LinearRing(ICoordinate[] coordinates) : base(coordinates)
    {
    }
    
    /// <summary>
    /// 좌표 리스트로부터 생성
    /// </summary>
    public LinearRing(List<ICoordinate> coordinates) : base(coordinates)
    {
    }
    
    /// <inheritdoc/>
    public override GeometryType GeometryType => GeometryType.LinearRing;
    
    /// <inheritdoc/>
    public override bool IsValid
    {
        get
        {
            // LinearRing은 반드시 폐곡선이어야 함
            if (!IsClosed) return false;
            
            // 최소 4개의 점 필요 (삼각형 + 폐곡)
            if (NumPoints < 4) return false;
            
            // 기본 유효성 검사
            return base.IsValid;
        }
    }
    
    /// <summary>
    /// 링이 시계방향인지 확인
    /// </summary>
    public bool IsClockwise()
    {
        return SignedArea() < 0;
    }
    
    /// <summary>
    /// 링이 반시계방향인지 확인
    /// </summary>
    public bool IsCounterClockwise()
    {
        return SignedArea() > 0;
    }
    
    /// <summary>
    /// 부호 있는 면적 계산 (양수: 반시계방향, 음수: 시계방향)
    /// </summary>
    public double SignedArea()
    {
        if (NumPoints < 3) return 0.0;
        
        double area = 0.0;
        var coords = Coordinates;
        
        for (int i = 0; i < coords.Length - 1; i++)
        {
            area += coords[i].X * coords[i + 1].Y - coords[i + 1].X * coords[i].Y;
        }
        
        return area / 2.0;
    }
    
    /// <inheritdoc/>
    public override double Area => Math.Abs(SignedArea());
    
    /// <inheritdoc/>
    public override IGeometry Copy()
    {
        return new LinearRing(Coordinates) { SRID = SRID };
    }
    
    /// <inheritdoc/>
    public override string ToText()
    {
        // LinearRing은 보통 Polygon의 일부로 표현되므로 LINESTRING으로 출력
        return base.ToText();
    }
    
    public override IGeometry Transform(object transformation)
    {
        // For now, just return a copy - actual transformation logic will be implemented later
        return new LinearRing(Coordinates) { SRID = SRID };
    }
    
}