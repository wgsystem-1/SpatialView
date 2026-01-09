namespace SpatialView.Engine.CoordinateSystems.Transformation;

/// <summary>
/// 헬머트 변환 인터페이스 (7-파라미터 변환)
/// </summary>
public interface IHelmertTransformation
{
    /// <summary>
    /// X축 변위 (미터)
    /// </summary>
    double DeltaX { get; }
    
    /// <summary>
    /// Y축 변위 (미터)
    /// </summary>
    double DeltaY { get; }
    
    /// <summary>
    /// Z축 변위 (미터)
    /// </summary>
    double DeltaZ { get; }
    
    /// <summary>
    /// X축 회전 (라디안)
    /// </summary>
    double RotationX { get; }
    
    /// <summary>
    /// Y축 회전 (라디안)
    /// </summary>
    double RotationY { get; }
    
    /// <summary>
    /// Z축 회전 (라디안)
    /// </summary>
    double RotationZ { get; }
    
    /// <summary>
    /// 스케일 팩터 (ppm)
    /// </summary>
    double ScaleFactor { get; }
    
    /// <summary>
    /// 역변환 가능 여부
    /// </summary>
    bool IsReversible { get; }
    
    // 레거시 호환성을 위한 속성들
    /// <summary>
    /// X축 변위 (레거시 호환성)
    /// </summary>
    double DX => DeltaX;
    
    /// <summary>
    /// Y축 변위 (레거시 호환성)
    /// </summary>
    double DY => DeltaY;
    
    /// <summary>
    /// Z축 변위 (레거시 호환성)
    /// </summary>
    double DZ => DeltaZ;
    
    /// <summary>
    /// X축 회전 (레거시 호환성)
    /// </summary>
    double RX => RotationX;
    
    /// <summary>
    /// Y축 회전 (레거시 호환성)
    /// </summary>
    double RY => RotationY;
    
    /// <summary>
    /// Z축 회전 (레거시 호환성)
    /// </summary>
    double RZ => RotationZ;
    
    /// <summary>
    /// 스케일 팩터 (레거시 호환성)
    /// </summary>
    double Scale => ScaleFactor;
    
    /// <summary>
    /// 좌표 변환
    /// </summary>
    /// <param name="x">X 좌표</param>
    /// <param name="y">Y 좌표</param>
    /// <param name="z">Z 좌표</param>
    /// <returns>변환된 좌표</returns>
    (double x, double y, double z) Transform(double x, double y, double z);
    
    /// <summary>
    /// 역변환
    /// </summary>
    /// <param name="x">X 좌표</param>
    /// <param name="y">Y 좌표</param>
    /// <param name="z">Z 좌표</param>
    /// <returns>역변환된 좌표</returns>
    (double x, double y, double z) InverseTransform(double x, double y, double z);
}