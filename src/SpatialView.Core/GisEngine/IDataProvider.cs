using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data;

namespace SpatialView.Core.GisEngine;

/// <summary>
/// 데이터 제공자 추상 인터페이스
/// IFeatureSource를 확장하여 SharpMap Provider와 호환 가능한 인터페이스 제공
/// </summary>
public interface IDataProvider : IFeatureSource
{
    /// <summary>
    /// 연결 문자열 (파일 경로 등)
    /// </summary>
    string ConnectionID { get; }
    
    /// <summary>
    /// 고유 ID 컬럼명
    /// </summary>
    string ObjectIdColumn { get; }
    
    /// <summary>
    /// 지오메트리 컬럼명
    /// </summary>
    string GeometryColumn { get; }
    
    /// <summary>
    /// 영역 내 객체 ID 목록 가져오기
    /// </summary>
    IList<uint> GetObjectIDsInView(Envelope bbox);
    
    /// <summary>
    /// ID별 지오메트리 가져오기
    /// </summary>
    new IGeometry? GetGeometryByID(uint oid);
    
    /// <summary>
    /// ID별 피처 가져오기
    /// </summary>
    IFeature? GetFeature(uint oid);
    
    /// <summary>
    /// 모든 지오메트리 가져오기
    /// </summary>
    IEnumerable<IGeometry> GetGeometries();
    
    /// <summary>
    /// 지정된 영역의 지오메트리 가져오기
    /// </summary>
    IEnumerable<IGeometry> GetGeometriesInView(Envelope bbox);
    
    /// <summary>
    /// 피처 테이블 (속성 데이터)
    /// </summary>
    System.Data.DataTable? GetFeatureTable();
    
    /// <summary>
    /// 범위 내 피처 테이블
    /// </summary>
    System.Data.DataTable? GetFeatureTableInView(Envelope bbox);
}