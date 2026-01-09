namespace SpatialView.Engine.CoordinateSystems.Transformation;

/// <summary>
/// 좌표 변환 인터페이스
/// </summary>
public interface ICoordinateTransformation
{
    /// <summary>
    /// 소스 좌표계
    /// </summary>
    ICoordinateSystem SourceCoordinateSystem { get; }
    
    /// <summary>
    /// 대상 좌표계
    /// </summary>
    ICoordinateSystem TargetCoordinateSystem { get; }
    
    /// <summary>
    /// 좌표 변환
    /// </summary>
    /// <param name="sourceCoordinate">소스 좌표</param>
    /// <returns>변환된 좌표</returns>
    Geometry.ICoordinate Transform(Geometry.ICoordinate sourceCoordinate);
    
    /// <summary>
    /// 좌표 배열 변환
    /// </summary>
    /// <param name="sourceCoordinates">소스 좌표 배열</param>
    /// <returns>변환된 좌표 배열</returns>
    Geometry.ICoordinate[] Transform(Geometry.ICoordinate[] sourceCoordinates);
    
    /// <summary>
    /// 지오메트리 변환
    /// </summary>
    /// <param name="geometry">소스 지오메트리</param>
    /// <returns>변환된 지오메트리</returns>
    Geometry.IGeometry Transform(Geometry.IGeometry geometry);
}