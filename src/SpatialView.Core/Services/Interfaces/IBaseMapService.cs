using SpatialView.Core.Models;

namespace SpatialView.Core.Services.Interfaces;

/// <summary>
/// 배경지도 서비스 인터페이스
/// 다양한 배경지도 타일 레이어를 제공합니다.
/// </summary>
public interface IBaseMapService
{
    /// <summary>
    /// OSM 타일 레이어 생성
    /// </summary>
    /// <returns>생성된 타일 레이어 (ILayer)</returns>
    object? CreateOsmLayer();
    
    /// <summary>
    /// Bing Maps 타일 레이어 생성
    /// </summary>
    /// <param name="apiKey">Bing Maps API Key</param>
    /// <param name="mapType">지도 유형 (Road, Aerial, Hybrid)</param>
    /// <returns>생성된 타일 레이어 (ILayer)</returns>
    object? CreateBingLayer(string apiKey, BaseMapType mapType = BaseMapType.BingRoad);
    
    /// <summary>
    /// 사용 가능한 배경지도 목록 조회
    /// </summary>
    /// <returns>배경지도 정보 목록</returns>
    List<BaseMapInfo> GetAvailableBaseMaps();
    
    /// <summary>
    /// 배경지도 유형에 따라 레이어 생성
    /// </summary>
    /// <param name="info">배경지도 정보</param>
    /// <returns>생성된 타일 레이어 (ILayer)</returns>
    object? CreateLayer(BaseMapInfo info);
    
    /// <summary>
    /// 현재 선택된 배경지도
    /// </summary>
    BaseMapInfo? CurrentBaseMap { get; }
}

