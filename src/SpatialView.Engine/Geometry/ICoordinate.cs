namespace SpatialView.Engine.Geometry;

/// <summary>
/// 좌표를 나타내는 인터페이스
/// </summary>
public interface ICoordinate
{
    /// <summary>
    /// X 좌표 (경도)
    /// </summary>
    double X { get; set; }
    
    /// <summary>
    /// Y 좌표 (위도)
    /// </summary>
    double Y { get; set; }
    
    /// <summary>
    /// Z 좌표 (고도) - 선택적
    /// </summary>
    double Z { get; set; }
    
    /// <summary>
    /// M 값 (측정값) - 선택적
    /// </summary>
    double M { get; set; }
    
    /// <summary>
    /// 2D 거리 계산
    /// </summary>
    double Distance(ICoordinate other);
    
    /// <summary>
    /// 3D 거리 계산
    /// </summary>
    double Distance3D(ICoordinate other);
    
    /// <summary>
    /// 좌표가 같은지 비교
    /// </summary>
    bool Equals2D(ICoordinate other);
    
    /// <summary>
    /// 좌표 복사
    /// </summary>
    ICoordinate Copy();
}