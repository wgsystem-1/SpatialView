using SpatialView.Core.GisEngine;

namespace SpatialView.Core.Factories;

/// <summary>
/// 타일 소스 생성 팩토리 인터페이스
/// </summary>
public interface ITileSourceFactory
{
    /// <summary>
    /// 타일 소스 생성
    /// </summary>
    ITileSource? CreateTileSource(string type, string urlTemplate);
}