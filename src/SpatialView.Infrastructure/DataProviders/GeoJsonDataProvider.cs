using System.Text;
using System.Text.Json;
using SpatialView.Core.Enums;
using SpatialView.Core.Models;
using SpatialView.Core.Services.Interfaces;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Geometry.IO;
using SpatialView.Engine.Data.Layers;

namespace SpatialView.Infrastructure.DataProviders;

/// <summary>
/// GeoJSON 데이터 Provider
/// NetTopologySuite.IO.GeoJSON을 사용하여 로드합니다.
/// </summary>
public class GeoJsonDataProvider : IDataProvider
{
    public string[] SupportedExtensions => new[] { ".geojson", ".json" };
    
    public string ProviderName => "GeoJSON";
    
    public bool IsFolderBased => false;
    
    /// <summary>
    /// 해당 파일을 로드할 수 있는지 확인
    /// </summary>
    public bool CanLoad(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;
        
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }
    
    /// <summary>
    /// GeoJSON을 비동기로 로드
    /// </summary>
    public async Task<LayerInfo> LoadAsync(string filePath)
    {
        return await Task.Run(() => LoadInternal(filePath));
    }
    
    /// <summary>
    /// GeoJSON 로드 내부 로직
    /// </summary>
    private LayerInfo LoadInternal(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"GeoJSON 파일을 찾을 수 없습니다: {filePath}");
        
        // UTF-8로 파일 읽기
        var json = File.ReadAllText(filePath, Encoding.UTF8);
        
        // GeoJSON 파싱
        // GeoJsonParser는 단일 지오메트리만 파싱하므로, JSON을 직접 파싱
        var geometries = new List<IGeometry>();
        using (var document = System.Text.Json.JsonDocument.Parse(json))
        {
            var root = document.RootElement;
            
            // FeatureCollection인 경우
            if (root.TryGetProperty("type", out var typeElement) && 
                typeElement.GetString() == "FeatureCollection" &&
                root.TryGetProperty("features", out var features))
            {
                foreach (var feature in features.EnumerateArray())
                {
                    if (feature.TryGetProperty("geometry", out var geometryElement))
                    {
                        var geometry = GeoJsonParser.ParseGeometry(geometryElement.ToString());
                        if (geometry != null)
                        {
                            geometries.Add(geometry);
                        }
                    }
                }
            }
            // 단일 Feature인 경우
            else if (typeElement.GetString() == "Feature")
            {
                if (root.TryGetProperty("geometry", out var geometryElement))
                {
                    var geometry = GeoJsonParser.ParseGeometry(geometryElement.ToString());
                    if (geometry != null)
                    {
                        geometries.Add(geometry);
                    }
                }
            }
            // 단일 Geometry인 경우
            else
            {
                var geometry = GeoJsonParser.ParseGeometry(json);
                if (geometry != null)
                {
                    geometries.Add(geometry);
                }
            }
        }
        
        if (geometries == null || geometries.Count == 0)
            throw new InvalidOperationException($"GeoJSON 파일에 피처가 없습니다: {filePath}");
        
        // 레이어 이름
        var layerName = Path.GetFileNameWithoutExtension(filePath);
        
        // 지오메트리 타입 결정
        var geometryType = DetermineGeometryType(geometries);
        
        // 메모리 기반 DataSource 생성
        var dataSource = new MemoryDataSource(geometries);
        
        // 범위 계산
        SpatialView.Engine.Geometry.Envelope? extent = null;
        if (geometries.Count > 0)
        {
            extent = new SpatialView.Engine.Geometry.Envelope(geometries[0].Envelope);
            foreach (var geom in geometries.Skip(1))
            {
                extent.ExpandToInclude(geom.Envelope);
            }
        }
        
        // VectorLayer 생성
        var vectorLayer = new VectorLayer
        {
            Name = layerName,
            DataSource = dataSource,
            Extent = extent  // 범위 직접 설정
        };
        
        return new LayerInfo
        {
            Id = Guid.NewGuid(),
            Name = layerName,
            FilePath = filePath,
            GeometryType = geometryType,
            FeatureCount = geometries.Count,
            Extent = extent,
            CRS = "EPSG:4326", // GeoJSON은 기본적으로 WGS84
            Layer = new Infrastructure.GisEngine.SpatialViewVectorLayerAdapter(vectorLayer)
        };
    }
    
    /// <summary>
    /// Geometry 리스트의 지오메트리 타입 결정
    /// </summary>
    private SpatialView.Core.Enums.GeometryType DetermineGeometryType(List<IGeometry> geometries)
    {
        if (geometries.Count == 0)
            return SpatialView.Core.Enums.GeometryType.Unknown;
        
        var firstGeom = geometries[0];
        if (firstGeom == null)
            return SpatialView.Core.Enums.GeometryType.Unknown;
        
        return firstGeom.GeometryType switch
        {
            SpatialView.Engine.Geometry.GeometryType.Point => SpatialView.Core.Enums.GeometryType.Point,
            SpatialView.Engine.Geometry.GeometryType.MultiPoint => SpatialView.Core.Enums.GeometryType.Point,
            SpatialView.Engine.Geometry.GeometryType.LineString => SpatialView.Core.Enums.GeometryType.Line,
            SpatialView.Engine.Geometry.GeometryType.MultiLineString => SpatialView.Core.Enums.GeometryType.Line,
            SpatialView.Engine.Geometry.GeometryType.Polygon => SpatialView.Core.Enums.GeometryType.Polygon,
            SpatialView.Engine.Geometry.GeometryType.MultiPolygon => SpatialView.Core.Enums.GeometryType.Polygon,
            SpatialView.Engine.Geometry.GeometryType.GeometryCollection => SpatialView.Core.Enums.GeometryType.GeometryCollection,
            _ => SpatialView.Core.Enums.GeometryType.Unknown
        };
    }
}
