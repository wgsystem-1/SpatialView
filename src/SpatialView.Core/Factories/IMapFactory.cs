using SpatialView.Core.GisEngine;

namespace SpatialView.Core.Factories;

/// <summary>
/// 맵 관련 객체 생성 팩토리 인터페이스
/// </summary>
public interface IMapFactory
{
    /// <summary>
    /// 새로운 맵 엔진 인스턴스 생성
    /// </summary>
    IMapEngine CreateMapEngine();
    
    /// <summary>
    /// 새로운 맵 렌더러 인스턴스 생성
    /// </summary>
    IMapRenderer CreateMapRenderer();
}