using SpatialView.Engine.Geometry;
using SpatialView.Core.Enums;

namespace SpatialView.Core.Models;

/// <summary>
/// 레이어 정보를 담는 모델 클래스
/// 로드된 데이터의 메타정보를 저장합니다.
/// </summary>
public class LayerInfo
{
    /// <summary>
    /// 레이어 고유 식별자
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// 레이어 이름
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 원본 파일 경로
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// 지오메트리 유형
    /// </summary>
    public Core.Enums.GeometryType GeometryType { get; set; } = Core.Enums.GeometryType.Unknown;
    
    /// <summary>
    /// 피처 개수
    /// </summary>
    public int FeatureCount { get; set; }
    
    /// <summary>
    /// 범위 (Envelope)
    /// </summary>
    public Envelope? Extent { get; set; }
    
    /// <summary>
    /// 좌표계 (예: "EPSG:4326")
    /// </summary>
    public string CRS { get; set; } = "EPSG:4326";
    
    /// <summary>
    /// 범위를 WKT 형식 문자열로 반환
    /// </summary>
    public string ExtentWkt => Extent != null 
        ? $"BBOX({Extent.MinX:F4}, {Extent.MinY:F4}, {Extent.MaxX:F4}, {Extent.MaxY:F4})"
        : "N/A";
    
    /// <summary>
    /// SharpMap Layer 객체 참조
    /// </summary>
    public object? Layer { get; set; }
}

