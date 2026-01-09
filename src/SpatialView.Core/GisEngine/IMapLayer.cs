using SpatialView.Engine.Geometry;

namespace SpatialView.Core.GisEngine;

/// <summary>
/// 지도 레이어 추상 인터페이스
/// ILayer를 상속하여 호환성 확보
/// </summary>
public interface IMapLayer : ILayer
{
    /// <summary>
    /// 레이어 활성화 여부 (SharpMap 호환)
    /// </summary>
    bool Enabled { get; set; }
    
    /// <summary>
    /// 좌표 참조 시스템 ID
    /// </summary>
    int SRID { get; set; }
    
    /// <summary>
    /// 레이어 타입
    /// </summary>
    LayerType LayerType { get; }
}

/// <summary>
/// 레이어 타입
/// </summary>
public enum LayerType
{
    /// <summary>
    /// 벡터 레이어
    /// </summary>
    Vector,
    
    /// <summary>
    /// 래스터 레이어
    /// </summary>
    Raster,
    
    /// <summary>
    /// 타일 레이어
    /// </summary>
    Tile,
    
    /// <summary>
    /// 라벨 레이어
    /// </summary>
    Label,
    
    /// <summary>
    /// 그룹 레이어
    /// </summary>
    Group
}

/// <summary>
/// 벡터 레이어 인터페이스
/// </summary>
public interface IVectorLayer : IMapLayer
{
    /// <summary>
    /// 데이터 소스
    /// </summary>
    IFeatureSource? DataSource { get; set; }
    
    /// <summary>
    /// 스타일
    /// </summary>
    Core.Styling.IVectorStyle? Style { get; set; }
    
    /// <summary>
    /// 데이터 제공자 (SharpMap 호환용)
    /// </summary>
    IDataProvider? Provider { get; set; }
}

/// <summary>
/// 타일 레이어 인터페이스
/// </summary>
public interface ITileLayer : IMapLayer
{
    /// <summary>
    /// 타일 소스
    /// </summary>
    ITileSource? TileSource { get; set; }
}

