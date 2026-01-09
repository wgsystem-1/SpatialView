using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Geometry3D;

/// <summary>
/// 3D 지오메트리 인터페이스
/// </summary>
public interface IGeometry3D : IGeometry
{
    /// <summary>
    /// Z 좌표의 최소값
    /// </summary>
    double MinZ { get; }
    
    /// <summary>
    /// Z 좌표의 최대값
    /// </summary>
    double MaxZ { get; }
    
    /// <summary>
    /// 3D Envelope
    /// </summary>
    Envelope3D? Envelope3D { get; }
    
    /// <summary>
    /// 3D 중심점
    /// </summary>
    Point3D? Centroid3D { get; }
    
    /// <summary>
    /// 2D 투영
    /// </summary>
    IGeometry ProjectTo2D();
    
    /// <summary>
    /// 3D 변환 적용
    /// </summary>
    IGeometry3D Transform(Matrix3D matrix);
}