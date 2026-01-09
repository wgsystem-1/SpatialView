using SpatialView.Core.GisEngine;

namespace SpatialView.Core.Factories;

/// <summary>
/// 레이어 생성 팩토리 인터페이스
/// </summary>
public interface ILayerFactory
{
    /// <summary>
    /// 벡터 레이어 생성
    /// </summary>
    IVectorLayer CreateVectorLayer(string name);
    
    /// <summary>
    /// 데이터 제공자를 사용한 벡터 레이어 생성
    /// </summary>
    IVectorLayer CreateVectorLayer(string name, IDataProvider provider);
    
    /// <summary>
    /// 타일 레이어 생성
    /// </summary>
    ITileLayer CreateTileLayer(string name, ITileSource tileSource);
    
    /// <summary>
    /// 하이라이트 레이어 생성
    /// </summary>
    IVectorLayer CreateHighlightLayer(string name = "Highlight");
}