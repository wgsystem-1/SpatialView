using SpatialView.Core.GisEngine;
using SpatialView.Core.Factories;

namespace SpatialView.Infrastructure.Factories;

/// <summary>
/// 타일 소스 생성 팩토리 구현
/// </summary>
public class TileSourceFactory : ITileSourceFactory
{
    /// <summary>
    /// 타일 소스 생성
    /// </summary>
    public ITileSource? CreateTileSource(string type, string urlTemplate)
    {
        // SharpMap 1.2.0에는 타일 소스가 없으므로 null 반환
        // 향후 구현 예정
        return null;
    }
}