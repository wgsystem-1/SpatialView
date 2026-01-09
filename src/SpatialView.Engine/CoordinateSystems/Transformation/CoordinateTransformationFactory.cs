namespace SpatialView.Engine.CoordinateSystems.Transformation;

/// <summary>
/// 좌표 변환 팩토리
/// </summary>
public class CoordinateTransformationFactory
{
    /// <summary>
    /// 두 좌표계 간의 변환 생성
    /// </summary>
    /// <param name="source">소스 좌표계</param>
    /// <param name="target">대상 좌표계</param>
    /// <returns>좌표 변환 객체</returns>
    public ICoordinateTransformation CreateTransformation(ICoordinateSystem source, ICoordinateSystem target)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (target == null) throw new ArgumentNullException(nameof(target));
        
        // 같은 좌표계면 항등 변환 반환
        if (source.AuthorityCode == target.AuthorityCode && source.Authority == target.Authority)
        {
            return new IdentityTransformation(source);
        }
        
        // 임시: 기본 수학 변환 구현 사용
        return new BasicMathTransformation(source, target);
    }
    
    /// <summary>
    /// SRID를 사용한 변환 생성
    /// </summary>
    public ICoordinateTransformation CreateTransformation(int sourceSRID, int targetSRID)
    {
        var source = GetCoordinateSystem(sourceSRID);
        var target = GetCoordinateSystem(targetSRID);
        return CreateTransformation(source, target);
    }
    
    private ICoordinateSystem GetCoordinateSystem(int srid)
    {
        // 주요 SRID에 대한 하드코딩된 좌표계 반환
        // 실제 구현에서는 EPSG 데이터베이스나 WKT 파일에서 읽어와야 함
        switch (srid)
        {
            case 4326:
                return WellKnownCoordinateSystems.WGS84;
            case 3857:
                return WellKnownCoordinateSystems.WebMercator;
            case 5186:
                return WellKnownCoordinateSystems.KoreaMiddleBelt;
            default:
                throw new NotSupportedException($"SRID {srid} is not supported yet.");
        }
    }
}

/// <summary>
/// 기본 수학 변환 임시 구현
/// </summary>
internal class BasicMathTransformation : MathTransformation
{
    public BasicMathTransformation(ICoordinateSystem source, ICoordinateSystem target) 
        : base(source, target) { }

    public override Geometry.ICoordinate Transform(Geometry.ICoordinate sourceCoordinate)
    {
        // 임시: 항등 변환 (실제로는 투영 계산 필요)
        return new Geometry.Coordinate(sourceCoordinate.X, sourceCoordinate.Y, sourceCoordinate.Z);
    }
}