using BruTile.Predefined;
using SpatialView.Core.Models;
using SpatialView.Core.Services.Interfaces;
using SpatialView.Engine.Data.Layers;

namespace SpatialView.Infrastructure.Services;

/// <summary>
/// 배경지도 서비스 구현
/// BruTile을 사용하여 타일 배경지도를 제공합니다.
/// </summary>
public class BaseMapService : IBaseMapService
{
    private readonly List<BaseMapInfo> _availableBaseMaps;
    
    public BaseMapInfo? CurrentBaseMap { get; private set; }
    
    public BaseMapService()
    {
        _availableBaseMaps = new List<BaseMapInfo>
        {
            new BaseMapInfo
            {
                Id = "none",
                Name = "없음",
                Type = BaseMapType.None,
                Description = "배경지도 없음"
            },
            new BaseMapInfo
            {
                Id = "osm",
                Name = "OpenStreetMap",
                Type = BaseMapType.OpenStreetMap,
                Description = "OpenStreetMap 기본 지도"
            },
            new BaseMapInfo
            {
                Id = "osm-cycle",
                Name = "OpenCycleMap",
                Type = BaseMapType.OpenCycleMap,
                Description = "OpenStreetMap 자전거 지도"
            }
        };
    }
    
    /// <summary>
    /// OpenStreetMap 타일 레이어 생성
    /// </summary>
    public object? CreateOsmLayer()
    {
        try
        {
            // BruTile의 KnownTileSource를 사용하여 OSM 타일 소스 생성
            var tileSource = KnownTileSources.Create(KnownTileSource.OpenStreetMap);
            
            // TODO: Engine에 TileLayer가 없으므로, 타일 레이어 지원 추가 필요
            // 임시로 RasterLayer 사용
            var tileLayer = new RasterLayer("OpenStreetMap")
            {
                SRID = 3857 // Web Mercator
            };
            
            return tileLayer;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OSM 레이어 생성 실패: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Bing Maps 타일 레이어 생성
    /// </summary>
    public object? CreateBingLayer(string apiKey, BaseMapType mapType = BaseMapType.BingRoad)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            System.Diagnostics.Debug.WriteLine("Bing Maps API Key가 필요합니다.");
            return null;
        }
        
        try
        {
            // Bing Maps 타일 소스 유형 결정
            var bingMapType = mapType switch
            {
                BaseMapType.BingAerial => "Aerial",
                BaseMapType.BingHybrid => "AerialWithLabels",
                _ => "Road"
            };
            
            // TODO: Bing Maps API 연동 구현
            // 현재는 placeholder
            System.Diagnostics.Debug.WriteLine($"Bing Maps ({bingMapType}) 레이어 생성 - API Key 필요");
            
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Bing Maps 레이어 생성 실패: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 사용 가능한 배경지도 목록 반환
    /// </summary>
    public List<BaseMapInfo> GetAvailableBaseMaps()
    {
        return _availableBaseMaps.ToList();
    }
    
    /// <summary>
    /// 배경지도 정보에 따라 레이어 생성
    /// </summary>
    public object? CreateLayer(BaseMapInfo info)
    {
        CurrentBaseMap = info;
        
        return info.Type switch
        {
            BaseMapType.None => null,
            BaseMapType.OpenStreetMap => CreateOsmLayer(),
            BaseMapType.OpenCycleMap => CreateOpenCycleMapLayer(),
            BaseMapType.BingRoad => CreateBingLayer(info.ApiKey ?? "", BaseMapType.BingRoad),
            BaseMapType.BingAerial => CreateBingLayer(info.ApiKey ?? "", BaseMapType.BingAerial),
            BaseMapType.BingHybrid => CreateBingLayer(info.ApiKey ?? "", BaseMapType.BingHybrid),
            _ => null
        };
    }
    
    /// <summary>
    /// OpenCycleMap 타일 레이어 생성
    /// </summary>
    private object? CreateOpenCycleMapLayer()
    {
        try
        {
            var tileSource = KnownTileSources.Create(KnownTileSource.OpenCycleMap);
            
            // TODO: Engine에 TileLayer가 없으므로, 타일 레이어 지원 추가 필요
            // 임시로 RasterLayer 사용
            var tileLayer = new RasterLayer("OpenCycleMap")
            {
                SRID = 3857
            };
            
            return tileLayer;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenCycleMap 레이어 생성 실패: {ex.Message}");
            return null;
        }
    }
}

