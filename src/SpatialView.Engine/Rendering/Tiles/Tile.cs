using System.Windows.Media;

namespace SpatialView.Engine.Rendering.Tiles;

/// <summary>
/// 타일 구현체
/// 웹 맵 타일 서비스 (WMTS, TMS 등)의 타일을 나타냅니다
/// </summary>
public class Tile : ITile
{
    /// <inheritdoc/>
    public int X { get; }
    
    /// <inheritdoc/>
    public int Y { get; }
    
    /// <inheritdoc/>
    public int ZoomLevel { get; }
    
    /// <inheritdoc/>
    public Geometry.Envelope Bounds { get; }
    
    /// <inheritdoc/>
    public ImageSource? Image { get; set; }
    
    /// <inheritdoc/>
    public TileLoadState LoadState { get; set; }
    
    /// <inheritdoc/>
    public string Id { get; }
    
    /// <inheritdoc/>
    public DateTime LastAccessed { get; set; }
    
    /// <summary>
    /// 타일 URL
    /// </summary>
    public string? Url { get; set; }
    
    /// <summary>
    /// 로딩 시작 시간
    /// </summary>
    public DateTime? LoadStartTime { get; set; }
    
    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="x">타일 X 좌표</param>
    /// <param name="y">타일 Y 좌표</param>
    /// <param name="zoomLevel">줌 레벨</param>
    /// <param name="bounds">타일 경계</param>
    public Tile(int x, int y, int zoomLevel, Geometry.Envelope bounds)
    {
        X = x;
        Y = y;
        ZoomLevel = zoomLevel;
        Bounds = bounds;
        Id = $"{zoomLevel}/{x}/{y}";
        LoadState = TileLoadState.NotLoaded;
        LastAccessed = DateTime.Now;
    }
    
    /// <summary>
    /// 생성자 (URL 포함)
    /// </summary>
    /// <param name="x">타일 X 좌표</param>
    /// <param name="y">타일 Y 좌표</param>
    /// <param name="zoomLevel">줌 레벨</param>
    /// <param name="bounds">타일 경계</param>
    /// <param name="url">타일 URL</param>
    public Tile(int x, int y, int zoomLevel, Geometry.Envelope bounds, string url)
        : this(x, y, zoomLevel, bounds)
    {
        Url = url;
    }
    
    /// <inheritdoc/>
    public override string ToString()
    {
        return $"Tile({X}, {Y}, {ZoomLevel}) - {LoadState}";
    }
    
    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is Tile other && Id == other.Id;
    }
    
    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}

/// <summary>
/// 타일 유틸리티 클래스
/// </summary>
public static class TileUtils
{
    /// <summary>
    /// 웹 메르카토르 좌표를 타일 좌표로 변환
    /// </summary>
    /// <param name="lon">경도</param>
    /// <param name="lat">위도</param>
    /// <param name="zoom">줌 레벨</param>
    /// <returns>타일 좌표 (X, Y)</returns>
    public static (int X, int Y) LatLonToTileXY(double lon, double lat, int zoom)
    {
        var n = Math.Pow(2, zoom);
        var x = (int)Math.Floor((lon + 180.0) / 360.0 * n);
        var y = (int)Math.Floor((1.0 - Math.Asinh(Math.Tan(lat * Math.PI / 180.0)) / Math.PI) / 2.0 * n);
        
        return (x, y);
    }
    
    /// <summary>
    /// 타일 좌표를 웹 메르카토르 좌표로 변환
    /// </summary>
    /// <param name="x">타일 X 좌표</param>
    /// <param name="y">타일 Y 좌표</param>
    /// <param name="zoom">줌 레벨</param>
    /// <returns>경계 영역</returns>
    public static Geometry.Envelope TileXYToEnvelope(int x, int y, int zoom)
    {
        var n = Math.Pow(2, zoom);
        
        var lonMin = x / n * 360.0 - 180.0;
        var lonMax = (x + 1) / n * 360.0 - 180.0;
        
        var latMax = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y / n))) * 180.0 / Math.PI;
        var latMin = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * (y + 1) / n))) * 180.0 / Math.PI;
        
        return new Geometry.Envelope(lonMin, lonMax, latMin, latMax);
    }
    
    /// <summary>
    /// 줌 레벨에서 해상도 계산 (미터/픽셀, 적도 기준)
    /// </summary>
    /// <param name="zoom">줌 레벨</param>
    /// <returns>해상도 (미터/픽셀)</returns>
    public static double GetResolution(int zoom)
    {
        // 웹 메르카토르에서의 해상도 (적도 기준)
        const double earthCircumference = 40075016.686; // 미터
        const int tileSize = 256; // 픽셀
        
        return earthCircumference / (tileSize * Math.Pow(2, zoom));
    }
    
    /// <summary>
    /// 경계 영역에 포함되는 타일 목록 계산
    /// </summary>
    /// <param name="envelope">경계 영역</param>
    /// <param name="zoom">줌 레벨</param>
    /// <returns>타일 목록</returns>
    public static IEnumerable<(int X, int Y)> GetTilesInEnvelope(Geometry.Envelope envelope, int zoom)
    {
        var topLeft = LatLonToTileXY(envelope.MinX, envelope.MaxY, zoom);
        var bottomRight = LatLonToTileXY(envelope.MaxX, envelope.MinY, zoom);
        
        var minX = Math.Max(0, Math.Min(topLeft.X, bottomRight.X));
        var maxX = Math.Min((1 << zoom) - 1, Math.Max(topLeft.X, bottomRight.X));
        var minY = Math.Max(0, Math.Min(topLeft.Y, bottomRight.Y));
        var maxY = Math.Min((1 << zoom) - 1, Math.Max(topLeft.Y, bottomRight.Y));
        
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                yield return (x, y);
            }
        }
    }
    
    /// <summary>
    /// 줌 레벨에서 최적의 타일 크기 계산
    /// </summary>
    /// <param name="zoom">줌 레벨</param>
    /// <param name="screenDpi">화면 DPI</param>
    /// <returns>화면에서의 타일 크기 (픽셀)</returns>
    public static double GetTileScreenSize(int zoom, double screenDpi = 96)
    {
        // 표준 256픽셀 타일을 기준으로 화면 DPI에 따라 조정
        const int standardTileSize = 256;
        const double standardDpi = 96;
        
        return standardTileSize * (screenDpi / standardDpi);
    }
}