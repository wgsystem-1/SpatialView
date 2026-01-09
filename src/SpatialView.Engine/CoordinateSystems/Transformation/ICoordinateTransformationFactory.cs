namespace SpatialView.Engine.CoordinateSystems.Transformation;

/// <summary>
/// 좌표 변환 팩토리 인터페이스
/// </summary>
public interface ICoordinateTransformationFactory
{
    /// <summary>
    /// 두 좌표계 간의 변환 생성
    /// </summary>
    /// <param name="sourceCS">소스 좌표계</param>
    /// <param name="targetCS">타겟 좌표계</param>
    /// <returns>좌표 변환</returns>
    ICoordinateTransformation CreateFromCoordinateSystems(ICoordinateSystem sourceCS, ICoordinateSystem targetCS);
    
    /// <summary>
    /// EPSG 코드를 사용한 변환 생성
    /// </summary>
    /// <param name="sourceSRID">소스 SRID</param>
    /// <param name="targetSRID">타겟 SRID</param>
    /// <returns>좌표 변환</returns>
    ICoordinateTransformation CreateFromEPSG(int sourceSRID, int targetSRID);
    
    /// <summary>
    /// 변환 가능 여부 확인
    /// </summary>
    /// <param name="sourceCS">소스 좌표계</param>
    /// <param name="targetCS">타겟 좌표계</param>
    /// <returns>변환 가능 여부</returns>
    bool CanTransform(ICoordinateSystem sourceCS, ICoordinateSystem targetCS);
    
    /// <summary>
    /// 두 좌표계 간의 변환 생성 (호환성 메서드)
    /// </summary>
    /// <param name="sourceCS">소스 좌표계</param>
    /// <param name="targetCS">타겟 좌표계</param>
    /// <returns>좌표 변환</returns>
    ICoordinateTransformation CreateTransformation(ICoordinateSystem sourceCS, ICoordinateSystem targetCS);
}