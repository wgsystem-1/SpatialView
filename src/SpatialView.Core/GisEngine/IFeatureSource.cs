using SpatialView.Engine.Geometry;

namespace SpatialView.Core.GisEngine;

/// <summary>
/// 피처 소스 인터페이스
/// SharpMap의 IProvider를 추상화
/// </summary>
public interface IFeatureSource : IDisposable
{
    /// <summary>
    /// 데이터 소스 열기
    /// </summary>
    void Open();
    
    /// <summary>
    /// 데이터 소스 닫기
    /// </summary>
    void Close();
    
    /// <summary>
    /// 연결 상태
    /// </summary>
    bool IsOpen { get; }
    
    /// <summary>
    /// 피처 개수
    /// </summary>
    int FeatureCount { get; }
    
    /// <summary>
    /// 데이터 범위
    /// </summary>
    Envelope GetExtents();
    
    /// <summary>
    /// ID로 지오메트리 가져오기
    /// </summary>
    IGeometry? GetGeometryByID(uint id);
    
    /// <summary>
    /// 범위 내 피처 ID 목록 가져오기
    /// </summary>
    IList<uint> GetFeaturesInView(Envelope envelope);
    
    /// <summary>
    /// 모든 피처 가져오기
    /// </summary>
    IEnumerable<IFeature> GetAllFeatures();
    
    /// <summary>
    /// ID로 피처 가져오기
    /// </summary>
    IFeature? GetFeatureByID(uint id);
    
    /// <summary>
    /// ID로 피처 가져오기 (SharpMap 호환성)
    /// </summary>
    IFeature? GetFeature(uint id);
    
    /// <summary>
    /// 범위 내 객체 ID 목록 가져오기 (SharpMap 호환성)
    /// </summary>
    IList<uint> GetObjectIDsInView(Envelope envelope);
    
    /// <summary>
    /// 좌표 참조 시스템 ID
    /// </summary>
    int SRID { get; set; }
}

/// <summary>
/// 피처 인터페이스
/// </summary>
public interface IFeature
{
    /// <summary>
    /// 피처 ID
    /// </summary>
    uint ID { get; }
    
    /// <summary>
    /// 지오메트리
    /// </summary>
    IGeometry? Geometry { get; }
    
    /// <summary>
    /// 속성값 가져오기
    /// </summary>
    object? GetAttribute(string name);
    
    /// <summary>
    /// 모든 속성 이름
    /// </summary>
    IEnumerable<string> AttributeNames { get; }
    
    /// <summary>
    /// 속성값 설정
    /// </summary>
    void SetAttribute(string name, object? value);
}

/// <summary>
/// 타일 소스 인터페이스
/// </summary>
public interface ITileSource
{
    /// <summary>
    /// 타일 스키마
    /// </summary>
    ITileSchema Schema { get; }
    
    /// <summary>
    /// 타일 가져오기
    /// </summary>
    byte[]? GetTile(TileInfo tileInfo);
    
    /// <summary>
    /// 타일 소스 이름
    /// </summary>
    string Name { get; }
}

/// <summary>
/// 타일 정보
/// </summary>
public class TileInfo
{
    public int Level { get; set; }
    public int Column { get; set; }
    public int Row { get; set; }
    public Envelope Extent { get; set; } = new Envelope();
}

/// <summary>
/// 타일 스키마 인터페이스
/// </summary>
public interface ITileSchema
{
    /// <summary>
    /// 좌표 참조 시스템
    /// </summary>
    string CRS { get; }
    
    /// <summary>
    /// 범위
    /// </summary>
    Envelope Extent { get; }
    
    /// <summary>
    /// 타일 너비
    /// </summary>
    int TileWidth { get; }
    
    /// <summary>
    /// 타일 높이
    /// </summary>
    int TileHeight { get; }
}