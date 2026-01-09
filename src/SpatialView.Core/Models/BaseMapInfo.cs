namespace SpatialView.Core.Models;

/// <summary>
/// 배경지도 정보를 담는 모델 클래스
/// </summary>
public class BaseMapInfo
{
    /// <summary>
    /// 배경지도 고유 식별자
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// 표시 이름
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 배경지도 유형
    /// </summary>
    public BaseMapType Type { get; set; }
    
    /// <summary>
    /// 설명
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// API Key (필요한 경우)
    /// </summary>
    public string? ApiKey { get; set; }
    
    /// <summary>
    /// 배경지도 활성화 여부
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 배경지도 유형
/// </summary>
public enum BaseMapType
{
    /// <summary>
    /// 없음
    /// </summary>
    None,
    
    /// <summary>
    /// OpenStreetMap
    /// </summary>
    OpenStreetMap,
    
    /// <summary>
    /// OpenStreetMap (Cycle Map)
    /// </summary>
    OpenCycleMap,
    
    /// <summary>
    /// Bing Maps Road
    /// </summary>
    BingRoad,
    
    /// <summary>
    /// Bing Maps Aerial
    /// </summary>
    BingAerial,
    
    /// <summary>
    /// Bing Maps Hybrid
    /// </summary>
    BingHybrid,
    
    /// <summary>
    /// 사용자 정의 타일 서비스
    /// </summary>
    Custom
}

