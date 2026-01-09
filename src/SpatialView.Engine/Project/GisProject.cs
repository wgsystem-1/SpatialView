using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data.Layers;
using SpatialView.Engine.Styling;

namespace SpatialView.Engine.Project;

/// <summary>
/// GIS 프로젝트 클래스
/// </summary>
public class GisProject
{
    private bool _isDirty;
    
    /// <summary>
    /// 프로젝트 ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 프로젝트 이름
    /// </summary>
    public string Name { get; set; } = "새 프로젝트";

    /// <summary>
    /// 프로젝트 설명
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 프로젝트 버전
    /// </summary>
    public Version Version { get; set; } = new Version(1, 0, 0, 0);

    /// <summary>
    /// 작성자
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// 생성 시간
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    /// <summary>
    /// 수정 시간
    /// </summary>
    public DateTime ModifiedDate { get; set; } = DateTime.Now;

    /// <summary>
    /// 프로젝트 좌표계 (EPSG 코드)
    /// </summary>
    public string? CoordinateSystem { get; set; } = "EPSG:4326";

    /// <summary>
    /// 초기 뷰 범위
    /// </summary>
    public Envelope? InitialExtent { get; set; }

    /// <summary>
    /// 초기 줌 레벨
    /// </summary>
    public double? InitialZoom { get; set; }

    /// <summary>
    /// 레이어 구성
    /// </summary>
    public List<LayerConfiguration> Layers { get; set; } = new();

    /// <summary>
    /// 기본 맵 설정
    /// </summary>
    public MapSettings MapSettings { get; set; } = new();

    /// <summary>
    /// 플러그인 설정
    /// </summary>
    public List<PluginConfiguration> Plugins { get; set; } = new();

    /// <summary>
    /// 사용자 정의 속성
    /// </summary>
    public Dictionary<string, object> CustomProperties { get; set; } = new();

    /// <summary>
    /// 프로젝트 파일 경로
    /// </summary>
    [JsonIgnore]
    public string? FilePath { get; set; }

    /// <summary>
    /// 변경 여부
    /// </summary>
    [JsonIgnore]
    public bool IsDirty 
    { 
        get => _isDirty;
        set
        {
            if (_isDirty != value)
            {
                _isDirty = value;
                if (_isDirty)
                {
                    ModifiedDate = DateTime.Now;
                }
            }
        }
    }

    /// <summary>
    /// JSON으로 저장
    /// </summary>
    public void SaveAsJson(string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(filePath, json);
        
        FilePath = filePath;
        IsDirty = false;
    }

    /// <summary>
    /// JSON에서 로드
    /// </summary>
    public static GisProject? LoadFromJson(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            };

            var project = JsonSerializer.Deserialize<GisProject>(json, options);
            if (project != null)
            {
                project.FilePath = filePath;
                project.IsDirty = false;
            }

            return project;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"프로젝트 로드 실패: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// XML로 저장
    /// </summary>
    public void SaveAsXml(string filePath)
    {
        var xml = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("GisProject",
                new XAttribute("version", Version.ToString()),
                new XElement("Metadata",
                    new XElement("Id", Id),
                    new XElement("Name", Name),
                    Description != null ? new XElement("Description", Description) : null,
                    new XElement("Author", Author ?? "Unknown"),
                    new XElement("CreatedDate", CreatedDate.ToString("O")),
                    new XElement("ModifiedDate", ModifiedDate.ToString("O"))
                ),
                new XElement("MapConfiguration",
                    new XElement("CoordinateSystem", CoordinateSystem),
                    InitialExtent != null ? SerializeEnvelope(InitialExtent) : null,
                    InitialZoom.HasValue ? new XElement("InitialZoom", InitialZoom.Value) : null,
                    SerializeMapSettings(MapSettings)
                ),
                new XElement("Layers", Layers.Select(SerializeLayer)),
                new XElement("Plugins", Plugins.Select(SerializePlugin)),
                CustomProperties.Count > 0 ? 
                    new XElement("CustomProperties", 
                        CustomProperties.Select(kv => new XElement("Property",
                            new XAttribute("name", kv.Key),
                            kv.Value?.ToString() ?? ""))) : null
            )
        );

        xml.Save(filePath);
        FilePath = filePath;
        IsDirty = false;
    }

    /// <summary>
    /// XML에서 로드
    /// </summary>
    public static GisProject? LoadFromXml(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var xml = XDocument.Load(filePath);
            var root = xml.Root;
            if (root == null || root.Name != "GisProject")
                return null;

            var project = new GisProject
            {
                FilePath = filePath,
                IsDirty = false
            };

            // 버전
            var versionAttr = root.Attribute("version");
            if (versionAttr != null && Version.TryParse(versionAttr.Value, out var version))
            {
                project.Version = version;
            }

            // 메타데이터
            var metadata = root.Element("Metadata");
            if (metadata != null)
            {
                project.Id = metadata.Element("Id")?.Value ?? Guid.NewGuid().ToString();
                project.Name = metadata.Element("Name")?.Value ?? "프로젝트";
                project.Description = metadata.Element("Description")?.Value;
                project.Author = metadata.Element("Author")?.Value;
                
                if (DateTime.TryParse(metadata.Element("CreatedDate")?.Value, out var created))
                    project.CreatedDate = created;
                if (DateTime.TryParse(metadata.Element("ModifiedDate")?.Value, out var modified))
                    project.ModifiedDate = modified;
            }

            // 맵 설정
            var mapConfig = root.Element("MapConfiguration");
            if (mapConfig != null)
            {
                project.CoordinateSystem = mapConfig.Element("CoordinateSystem")?.Value;
                project.InitialExtent = DeserializeEnvelope(mapConfig.Element("InitialExtent"));
                
                if (double.TryParse(mapConfig.Element("InitialZoom")?.Value, out var zoom))
                    project.InitialZoom = zoom;
                    
                var settings = DeserializeMapSettings(mapConfig.Element("MapSettings"));
                if (settings != null)
                    project.MapSettings = settings;
            }

            // 레이어
            var layers = root.Element("Layers");
            if (layers != null)
            {
                project.Layers = layers.Elements("Layer")
                    .Select(DeserializeLayer)
                    .Where(l => l != null)
                    .Cast<LayerConfiguration>()
                    .ToList();
            }

            // 플러그인
            var plugins = root.Element("Plugins");
            if (plugins != null)
            {
                project.Plugins = plugins.Elements("Plugin")
                    .Select(DeserializePlugin)
                    .Where(p => p != null)
                    .Cast<PluginConfiguration>()
                    .ToList();
            }

            // 사용자 정의 속성
            var customProps = root.Element("CustomProperties");
            if (customProps != null)
            {
                foreach (var prop in customProps.Elements("Property"))
                {
                    var name = prop.Attribute("name")?.Value;
                    if (name != null)
                    {
                        project.CustomProperties[name] = prop.Value;
                    }
                }
            }

            return project;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"XML 프로젝트 로드 실패: {ex.Message}");
            return null;
        }
    }

    #region XML Serialization Helpers

    private static XElement SerializeEnvelope(Envelope envelope)
    {
        return new XElement("InitialExtent",
            new XElement("MinX", envelope.MinX),
            new XElement("MinY", envelope.MinY),
            new XElement("MaxX", envelope.MaxX),
            new XElement("MaxY", envelope.MaxY)
        );
    }

    private static Envelope? DeserializeEnvelope(XElement? element)
    {
        if (element == null) return null;

        if (double.TryParse(element.Element("MinX")?.Value, out var minX) &&
            double.TryParse(element.Element("MinY")?.Value, out var minY) &&
            double.TryParse(element.Element("MaxX")?.Value, out var maxX) &&
            double.TryParse(element.Element("MaxY")?.Value, out var maxY))
        {
            return new Envelope(minX, minY, maxX, maxY);
        }

        return null;
    }

    private static XElement SerializeMapSettings(MapSettings settings)
    {
        return new XElement("MapSettings",
            new XElement("BackgroundColor", settings.BackgroundColor),
            new XElement("SelectionColor", settings.SelectionColor),
            new XElement("HighlightColor", settings.HighlightColor),
            new XElement("EnableAntialiasing", settings.EnableAntialiasing),
            new XElement("EnableLabels", settings.EnableLabels),
            new XElement("MinScale", settings.MinScale),
            new XElement("MaxScale", settings.MaxScale)
        );
    }

    private static MapSettings? DeserializeMapSettings(XElement? element)
    {
        if (element == null) return null;

        var settings = new MapSettings
        {
            BackgroundColor = element.Element("BackgroundColor")?.Value ?? "#FFFFFF",
            SelectionColor = element.Element("SelectionColor")?.Value ?? "#00FF00",
            HighlightColor = element.Element("HighlightColor")?.Value ?? "#FFFF00"
        };

        if (bool.TryParse(element.Element("EnableAntialiasing")?.Value, out var aa))
            settings.EnableAntialiasing = aa;
        if (bool.TryParse(element.Element("EnableLabels")?.Value, out var labels))
            settings.EnableLabels = labels;
        if (double.TryParse(element.Element("MinScale")?.Value, out var minScale))
            settings.MinScale = minScale;
        if (double.TryParse(element.Element("MaxScale")?.Value, out var maxScale))
            settings.MaxScale = maxScale;

        return settings;
    }

    private static XElement SerializeLayer(LayerConfiguration layer)
    {
        return new XElement("Layer",
            new XAttribute("type", layer.Type.ToString()),
            new XElement("Id", layer.Id),
            new XElement("Name", layer.Name),
            new XElement("DataSource", layer.DataSource),
            new XElement("IsVisible", layer.IsVisible),
            new XElement("IsSelectable", layer.IsSelectable),
            new XElement("IsEditable", layer.IsEditable),
            new XElement("MinScale", layer.MinScale),
            new XElement("MaxScale", layer.MaxScale),
            new XElement("Opacity", layer.Opacity),
            layer.Style != null ? SerializeStyle(layer.Style) : null,
            layer.LabelStyle != null ? SerializeLabelStyle(layer.LabelStyle) : null,
            layer.Filter != null ? new XElement("Filter", layer.Filter) : null,
            layer.Properties.Count > 0 ? 
                new XElement("Properties",
                    layer.Properties.Select(kv => new XElement("Property",
                        new XAttribute("name", kv.Key),
                        kv.Value?.ToString() ?? ""))) : null
        );
    }

    private static LayerConfiguration? DeserializeLayer(XElement element)
    {
        if (element == null) return null;

        var layer = new LayerConfiguration
        {
            Id = element.Element("Id")?.Value ?? Guid.NewGuid().ToString(),
            Name = element.Element("Name")?.Value ?? "레이어",
            DataSource = element.Element("DataSource")?.Value ?? ""
        };

        if (Enum.TryParse<LayerType>(element.Attribute("type")?.Value, out var type))
            layer.Type = type;

        if (bool.TryParse(element.Element("IsVisible")?.Value, out var visible))
            layer.IsVisible = visible;
        if (bool.TryParse(element.Element("IsSelectable")?.Value, out var selectable))
            layer.IsSelectable = selectable;
        if (bool.TryParse(element.Element("IsEditable")?.Value, out var editable))
            layer.IsEditable = editable;

        if (double.TryParse(element.Element("MinScale")?.Value, out var minScale))
            layer.MinScale = minScale;
        if (double.TryParse(element.Element("MaxScale")?.Value, out var maxScale))
            layer.MaxScale = maxScale;
        if (double.TryParse(element.Element("Opacity")?.Value, out var opacity))
            layer.Opacity = opacity;

        layer.Style = DeserializeStyle(element.Element("Style"));
        layer.LabelStyle = DeserializeLabelStyle(element.Element("LabelStyle"));
        layer.Filter = element.Element("Filter")?.Value;

        var properties = element.Element("Properties");
        if (properties != null)
        {
            foreach (var prop in properties.Elements("Property"))
            {
                var name = prop.Attribute("name")?.Value;
                if (name != null)
                {
                    layer.Properties[name] = prop.Value;
                }
            }
        }

        return layer;
    }

    private static XElement? SerializeStyle(StyleConfiguration style)
    {
        return new XElement("Style",
            new XAttribute("type", style.Type.ToString()),
            new XElement("FillColor", style.FillColor),
            new XElement("StrokeColor", style.StrokeColor),
            new XElement("StrokeWidth", style.StrokeWidth),
            style.Symbol != null ? new XElement("Symbol", style.Symbol) : null,
            style.Size.HasValue ? new XElement("Size", style.Size.Value) : null
        );
    }

    private static StyleConfiguration? DeserializeStyle(XElement? element)
    {
        if (element == null) return null;

        var style = new StyleConfiguration
        {
            FillColor = element.Element("FillColor")?.Value,
            StrokeColor = element.Element("StrokeColor")?.Value,
            Symbol = element.Element("Symbol")?.Value
        };

        if (Enum.TryParse<StyleType>(element.Attribute("type")?.Value, out var type))
            style.Type = type;
        if (double.TryParse(element.Element("StrokeWidth")?.Value, out var width))
            style.StrokeWidth = width;
        if (double.TryParse(element.Element("Size")?.Value, out var size))
            style.Size = size;

        return style;
    }

    private static XElement? SerializeLabelStyle(LabelStyleConfiguration labelStyle)
    {
        return new XElement("LabelStyle",
            new XElement("FieldName", labelStyle.FieldName),
            new XElement("FontFamily", labelStyle.FontFamily),
            new XElement("FontSize", labelStyle.FontSize),
            new XElement("FontColor", labelStyle.FontColor),
            new XElement("HaloColor", labelStyle.HaloColor),
            new XElement("HaloWidth", labelStyle.HaloWidth),
            new XElement("Placement", labelStyle.Placement.ToString())
        );
    }

    private static LabelStyleConfiguration? DeserializeLabelStyle(XElement? element)
    {
        if (element == null) return null;

        var style = new LabelStyleConfiguration
        {
            FieldName = element.Element("FieldName")?.Value ?? "",
            FontFamily = element.Element("FontFamily")?.Value ?? "Arial",
            FontColor = element.Element("FontColor")?.Value ?? "#000000",
            HaloColor = element.Element("HaloColor")?.Value
        };

        if (double.TryParse(element.Element("FontSize")?.Value, out var fontSize))
            style.FontSize = fontSize;
        if (double.TryParse(element.Element("HaloWidth")?.Value, out var haloWidth))
            style.HaloWidth = haloWidth;
        if (Enum.TryParse<LabelPlacement>(element.Element("Placement")?.Value, out var placement))
            style.Placement = placement;

        return style;
    }

    private static XElement SerializePlugin(PluginConfiguration plugin)
    {
        return new XElement("Plugin",
            new XElement("Id", plugin.Id),
            new XElement("Name", plugin.Name),
            new XElement("IsEnabled", plugin.IsEnabled),
            plugin.Settings != null ? 
                new XElement("Settings", JsonSerializer.Serialize(plugin.Settings)) : null
        );
    }

    private static PluginConfiguration? DeserializePlugin(XElement element)
    {
        if (element == null) return null;

        var plugin = new PluginConfiguration
        {
            Id = element.Element("Id")?.Value ?? "",
            Name = element.Element("Name")?.Value ?? ""
        };

        if (bool.TryParse(element.Element("IsEnabled")?.Value, out var enabled))
            plugin.IsEnabled = enabled;

        var settingsJson = element.Element("Settings")?.Value;
        if (!string.IsNullOrEmpty(settingsJson))
        {
            try
            {
                plugin.Settings = JsonSerializer.Deserialize<Dictionary<string, object>>(settingsJson);
            }
            catch
            {
                // 설정 파싱 실패 시 무시
            }
        }

        return plugin;
    }

    #endregion
}