using System.Windows.Media;

namespace SpatialView.Engine.Rendering.Tiles;

/// <summary>
/// 타일 렌더러 인터페이스
/// 래스터 타일을 효율적으로 렌더링합니다
/// </summary>
public interface ITileRenderer
{
    /// <summary>
    /// 타일 컬렉션 렌더링
    /// </summary>
    void RenderTiles(IEnumerable<ITile> tiles, RenderContext context);
    
    /// <summary>
    /// 단일 타일 렌더링
    /// </summary>
    void RenderTile(ITile tile, RenderContext context);
    
    /// <summary>
    /// 타일 캐싱 활성화 여부
    /// </summary>
    bool EnableCaching { get; set; }
    
    /// <summary>
    /// 최대 캐시 크기 (메가바이트)
    /// </summary>
    int MaxCacheSizeMB { get; set; }
    
    /// <summary>
    /// 캐시 정리
    /// </summary>
    void ClearCache();
}

/// <summary>
/// 타일 정보 인터페이스
/// </summary>
public interface ITile
{
    /// <summary>
    /// 타일 X 좌표 (타일 인덱스)
    /// </summary>
    int X { get; }
    
    /// <summary>
    /// 타일 Y 좌표 (타일 인덱스)
    /// </summary>
    int Y { get; }
    
    /// <summary>
    /// 줌 레벨
    /// </summary>
    int ZoomLevel { get; }
    
    /// <summary>
    /// 타일 경계 (지리적 좌표)
    /// </summary>
    Geometry.Envelope Bounds { get; }
    
    /// <summary>
    /// 타일 이미지
    /// </summary>
    ImageSource? Image { get; }
    
    /// <summary>
    /// 로딩 상태
    /// </summary>
    TileLoadState LoadState { get; }
    
    /// <summary>
    /// 타일 ID (캐싱용)
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// 마지막 액세스 시간
    /// </summary>
    DateTime LastAccessed { get; set; }
}

/// <summary>
/// 타일 로딩 상태
/// </summary>
public enum TileLoadState
{
    /// <summary>
    /// 아직 로딩되지 않음
    /// </summary>
    NotLoaded,
    
    /// <summary>
    /// 로딩 중
    /// </summary>
    Loading,
    
    /// <summary>
    /// 로딩 완료
    /// </summary>
    Loaded,
    
    /// <summary>
    /// 로딩 실패
    /// </summary>
    Failed
}