using System.Text.Json.Serialization;

namespace SpatialView.Core.Models;

/// <summary>
/// 프로젝트 파일 모델 (.svproj)
/// </summary>
public class ProjectFile
{
    /// <summary>
    /// 파일 형식 버전
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";
    
    /// <summary>
    /// 프로젝트 이름
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Untitled";
    
    /// <summary>
    /// 생성 일시
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 마지막 수정 일시
    /// </summary>
    [JsonPropertyName("modified")]
    public DateTime Modified { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 지도 설정
    /// </summary>
    [JsonPropertyName("mapSettings")]
    public MapSettings MapSettings { get; set; } = new();
    
    /// <summary>
    /// 레이어 목록
    /// </summary>
    [JsonPropertyName("layers")]
    public List<LayerSettings> Layers { get; set; } = new();
}

/// <summary>
/// 지도 설정
/// </summary>
public class MapSettings
{
    /// <summary>
    /// 지도 중심 X 좌표
    /// </summary>
    [JsonPropertyName("centerX")]
    public double CenterX { get; set; }
    
    /// <summary>
    /// 지도 중심 Y 좌표
    /// </summary>
    [JsonPropertyName("centerY")]
    public double CenterY { get; set; }
    
    /// <summary>
    /// 줌 레벨 (Map.Zoom)
    /// </summary>
    [JsonPropertyName("zoom")]
    public double Zoom { get; set; }
    
    /// <summary>
    /// 좌표계 (예: "EPSG:4326")
    /// </summary>
    [JsonPropertyName("crs")]
    public string CRS { get; set; } = "EPSG:4326";
    
    /// <summary>
    /// 배경지도 타입
    /// </summary>
    [JsonPropertyName("baseMapType")]
    public string? BaseMapType { get; set; }
    
    /// <summary>
    /// 배경지도 활성화 여부
    /// </summary>
    [JsonPropertyName("baseMapEnabled")]
    public bool BaseMapEnabled { get; set; }
}

/// <summary>
/// 레이어 설정
/// </summary>
public class LayerSettings
{
    /// <summary>
    /// 레이어 ID
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// 레이어 이름
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 소스 파일 경로 (상대 경로)
    /// </summary>
    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = string.Empty;
    
    /// <summary>
    /// 표시 여부
    /// </summary>
    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;
    
    /// <summary>
    /// 투명도 (0.0 ~ 1.0)
    /// </summary>
    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 1.0;
    
    /// <summary>
    /// 레이어 순서 (0이 맨 아래)
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }
    
    /// <summary>
    /// 레이어 색상 (ARGB)
    /// </summary>
    [JsonPropertyName("color")]
    public string? Color { get; set; }
}

