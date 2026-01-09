using SpatialView.Engine.Rendering.Tiles;

namespace SpatialView.Engine.Data.Layers;

/// <summary>
/// 타일 레이어 인터페이스
/// 웹 타일 서비스 (TMS, WMTS, OSM 등)를 위한 레이어
/// </summary>
public interface ITileLayer : ILayer
{
    /// <summary>
    /// 타일 서비스 URL 템플릿
    /// 예: "https://tile.openstreetmap.org/{z}/{x}/{y}.png"
    /// </summary>
    string UrlTemplate { get; set; }
    
    /// <summary>
    /// 최소 줌 레벨
    /// </summary>
    int MinZoomLevel { get; set; }
    
    /// <summary>
    /// 최대 줌 레벨
    /// </summary>
    int MaxZoomLevel { get; set; }
    
    /// <summary>
    /// 타일 크기 (픽셀)
    /// </summary>
    int TileSize { get; set; }
    
    /// <summary>
    /// 타일 요청 시 사용자 에이전트
    /// </summary>
    string UserAgent { get; set; }
    
    /// <summary>
    /// 동시 다운로드 수 제한
    /// </summary>
    int MaxConcurrentDownloads { get; set; }
    
    /// <summary>
    /// 타일 캐싱 활성화 여부
    /// </summary>
    bool EnableCaching { get; set; }
    
    /// <summary>
    /// 로컬 캐시 경로
    /// </summary>
    string? CachePath { get; set; }
    
    /// <summary>
    /// 캐시 만료 시간 (시간)
    /// </summary>
    int CacheExpirationHours { get; set; }
    
    /// <summary>
    /// 특정 영역과 줌 레벨의 타일 가져오기
    /// </summary>
    IEnumerable<ITile> GetTiles(Geometry.Envelope extent, int zoomLevel);
    
    /// <summary>
    /// 타일 URL 생성
    /// </summary>
    string GetTileUrl(int x, int y, int z);
    
    /// <summary>
    /// 타일 비동기 로드
    /// </summary>
    Task<ITile> LoadTileAsync(int x, int y, int z, CancellationToken cancellationToken = default);
}