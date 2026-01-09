using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SpatialView.Core.Enums;
using SpatialView.Core.Models;
using SpatialView.Core.Services.Interfaces;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Geometry.IO;
using SpatialView.Engine.Data.Layers;
using SpatialView.Engine.Extensions;
using SpatialView.Infrastructure.GisEngine;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
using EngineEnvelope = SpatialView.Engine.Geometry.Envelope;
using EngineMemoryDataSource = SpatialView.Engine.Data.Sources.MemoryDataSource;
using EngineFeature = SpatialView.Engine.Data.Feature;
using CoreGeometryType = SpatialView.Core.Enums.GeometryType;

namespace SpatialView.Infrastructure.DataProviders;

/// <summary>
/// Esri FileGDB (.gdb) 데이터 Provider
/// GDAL/OGR의 OpenFileGDB Driver를 사용하여 로드합니다.
/// </summary>
public class FileGdbDataProvider : IDataProvider
{
    public string[] SupportedExtensions => new[] { ".gdb" };
    
    public string ProviderName => "File Geodatabase";
    
    public bool IsFolderBased => true;
    
    private static string? _gdalError;
    private static bool _gdalReady;
    
    /// <summary>
    /// GDAL이 초기화되었는지 확인
    /// </summary>
    private static bool EnsureGdal()
    {
        if (_gdalReady) return true;
        
        if (GdalSupport.Initialize())
        {
            _gdalError = null;
            _gdalReady = true;
            return true;
        }
        
        _gdalError = GdalSupport.GetLastError() ?? "GDAL initialization failed.";
        _gdalReady = false;
        return false;
    }
    
    /// <summary>
    /// GDAL 오류 메시지 반환
    /// </summary>
    public static string? GetGdalError() => _gdalError;
    
    /// <summary>
    /// 해당 경로를 로드할 수 있는지 확인
    /// FileGDB는 폴더이므로 .gdb 폴더인지 확인
    /// </summary>
    public bool CanLoad(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;
        
        // .gdb 폴더인 경우
        if (Directory.Exists(filePath) && 
            filePath.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// FileGDB를 비동기로 로드 (첫 번째 레이어)
    /// </summary>
    public async Task<LayerInfo> LoadAsync(string filePath)
    {
        return await Task.Run(() => LoadLayerInternal(filePath, 0));
    }
    
    /// <summary>
    /// FileGDB의 특정 레이어를 비동기로 로드
    /// </summary>
    public async Task<LayerInfo> LoadLayerAsync(string gdbPath, int layerIndex)
    {
        return await Task.Run(() => LoadLayerInternal(gdbPath, layerIndex));
    }
    
    /// <summary>
    /// FileGDB의 특정 레이어를 이름으로 비동기 로드
    /// </summary>
    public async Task<LayerInfo?> LoadLayerByNameAsync(string gdbPath, string layerName)
    {
        return await Task.Run(() =>
        {
            var layerInfos = GetLayersInfo(gdbPath);
            var layerInfo = layerInfos.FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
            
            if (layerInfo == null)
            {
                System.Diagnostics.Debug.WriteLine($"레이어를 찾을 수 없음: {layerName}");
                return null;
            }
            
            return LoadLayerInternal(gdbPath, layerInfo.Index);
        });
    }
    
    /// <summary>
    /// FileGDB의 여러 레이어를 이름으로 비동기 로드
    /// </summary>
    public async Task<List<LayerInfo>> LoadLayersByNamesAsync(string gdbPath, IEnumerable<string> layerNames)
    {
        var results = new List<LayerInfo>();
        var layerInfos = GetLayersInfo(gdbPath);
        var namesList = layerNames.ToList();
        
        System.Diagnostics.Debug.WriteLine($"LoadLayersByNamesAsync: GDB={gdbPath}");
        System.Diagnostics.Debug.WriteLine($"  요청된 레이어: {string.Join(", ", namesList)}");
        System.Diagnostics.Debug.WriteLine($"  GDB 내 레이어: {string.Join(", ", layerInfos.Select(l => l.Name))}");
        
        foreach (var name in namesList)
        {
            var layerInfo = layerInfos.FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            
            if (layerInfo == null)
            {
                System.Diagnostics.Debug.WriteLine($"  레이어를 찾을 수 없음: {name}");
                continue;
            }
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"  레이어 로드 시작: {name} (Index={layerInfo.Index})");
                var loaded = await Task.Run(() => LoadLayerInternal(gdbPath, layerInfo.Index));
                results.Add(loaded);
                System.Diagnostics.Debug.WriteLine($"  레이어 로드 성공: {name}");
                await Task.Delay(10); // UI 응답성
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"  레이어 '{name}' 로드 실패: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"    스택: {ex.StackTrace}");
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"  로드 완료: {results.Count}/{namesList.Count}개");
        return results;
    }
    
    /// <summary>
    /// FileGDB의 여러 레이어를 비동기로 로드
    /// </summary>
    public async Task<List<LayerInfo>> LoadLayersAsync(string gdbPath, int[] layerIndices)
    {
        var results = new List<LayerInfo>();
        
        foreach (var index in layerIndices)
        {
            try
            {
                // 각 레이어를 별도의 Task로 로드하여 UI 응답성 유지
                var layerInfo = await Task.Run(() => LoadLayerInternal(gdbPath, index));
                results.Add(layerInfo);
                
                // UI 업데이트를 위해 잠시 양보
                await Task.Delay(10);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"레이어 {index} 로드 실패: {ex.Message}");
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// FileGDB 로드 내부 로직
    /// </summary>
    private LayerInfo LoadLayerInternal(string gdbPath, int layerIndex)
    {
        System.Diagnostics.Debug.WriteLine($"FileGDB 로드 시작: {gdbPath}");
        
        if (!EnsureGdal())
        {
            var errorMsg = _gdalError ?? "알 수 없는 오류";
            throw new InvalidOperationException(
                $"GDAL 드라이버를 사용할 수 없습니다: {errorMsg}\n\n" +
                $"FileGDB를 로드하려면 GDAL 설치가 필요합니다.");
        }
        
        if (!Directory.Exists(gdbPath))
            throw new DirectoryNotFoundException($"지정한 GDB 폴더를 찾을 수 없습니다: {gdbPath}");
        
        using var dataset = Ogr.Open(gdbPath, 0);
        if (dataset == null)
            throw new InvalidOperationException($"FileGDB를 열 수 없습니다: {gdbPath}");
        
        if (layerIndex < 0 || layerIndex >= dataset.GetLayerCount())
            throw new ArgumentOutOfRangeException(nameof(layerIndex), "유효하지 않은 레이어 인덱스입니다.");
        
        using var layer = dataset.GetLayerByIndex(layerIndex);
        if (layer == null)
            throw new InvalidOperationException($"레이어를 가져올 수 없습니다 (index {layerIndex})");
        
        layer.ResetReading();
        
        var layerName = layer.GetName() ?? $"Layer_{layerIndex}";
        var ogrGeomType = layer.GetGeomType();
        var geometryType = ConvertGeometryType((int)ogrGeomType);
        
        // 좌표계 추출
        int srid = 4326;
        var spatialRef = layer.GetSpatialRef();
        if (spatialRef != null)
        {
            srid = TryGetSrid(spatialRef) ?? 4326;
        }
        
        // 지오메트리 타입 확인 (테이블인 경우 wkbNone)
        bool isTable = (ogrGeomType == wkbGeometryType.wkbNone || ogrGeomType == wkbGeometryType.wkbUnknown);
        
        // 메모리 데이터소스에 적재
        var memoryDataSource = new EngineMemoryDataSource(layerName, srid);
        // 테이블(비공간 데이터)인 경우도 처리
        bool tableCreated;
        if (isTable)
        {
            // 테이블의 경우 지오메트리 없이 테이블 생성
            tableCreated = memoryDataSource.CreateTable(layerName, "None", srid);
        }
        else
        {
            tableCreated = memoryDataSource.CreateTable(layerName, ogrGeomType.ToString(), srid);
        }
        
        if (!tableCreated)
        {
            throw new InvalidOperationException($"테이블 '{layerName}' 생성 실패");
        }
        
        memoryDataSource.Open();
        
        var extent = ReadExtent(layer);
        var featureCount = (int)layer.GetFeatureCount(1);
        
        System.Diagnostics.Debug.WriteLine($"FileGDB 레이어 '{layerName}': 피처 수 = {featureCount}");
        
        OSGeo.OGR.Feature? ogrFeature = null;
        int loadedCount = 0;
        int errorCount = 0;
        const int progressInterval = 1000; // 1000개마다 진행 상황 로깅
        
        try
        {
            layer.ResetReading(); // 읽기 위치 초기화
            while ((ogrFeature = layer.GetNextFeature()) != null)
            {
                try
                {
                    var engineFeature = ConvertFeature(ogrFeature);
                    // 메모리 데이터소스 삽입 (동기 호출)
                    var insertResult = memoryDataSource.InsertFeatureAsync(layerName, engineFeature).GetAwaiter().GetResult();
                    if (insertResult)
                    {
                        loadedCount++;
                        
                        // 진행 상황 로깅
                        if (loadedCount % progressInterval == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"  로딩 중... {loadedCount}/{featureCount} ({(loadedCount * 100 / Math.Max(1, featureCount))}%)");
                        }
                    }
                    else
                    {
                        errorCount++;
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    if (errorCount <= 5) // 처음 5개 오류만 로깅
                    {
                        System.Diagnostics.Debug.WriteLine($"피처 변환/삽입 오류 (FID={ogrFeature.GetFID()}): {ex.Message}");
                    }
                }
                finally
                {
                    ogrFeature.Dispose();
                    ogrFeature = null;
                }
            }
        }
        finally
        {
            ogrFeature?.Dispose();
        }
        
        System.Diagnostics.Debug.WriteLine($"FileGDB 레이어 '{layerName}': 로드 완료 = {loadedCount}/{featureCount} (오류: {errorCount})");
        
        // VectorLayer 및 어댑터 구성
        var vectorLayer = new VectorLayer
        {
            Name = layerName,
            DataSource = memoryDataSource,
            TableName = layerName,
            SRID = srid,
            Extent = extent  // 범위 직접 설정
        };
        
        // 피처 캐시 로드 (GetFeatures 호출 시 다시 로드하지 않도록)
        vectorLayer.Refresh();
        
        // Refresh 후에도 Extent가 null이면 직접 설정
        if (vectorLayer.Extent == null && extent != null)
        {
            vectorLayer.Extent = extent;
        }
        
        System.Diagnostics.Debug.WriteLine($"FileGDB 레이어 '{layerName}': Extent = {vectorLayer.Extent}");
        
        var featureSource = new EngineDataSourceFeatureSourceAdapter(memoryDataSource, layerName);
        var layerAdapter = new SpatialViewVectorLayerAdapter(vectorLayer)
        {
            DataSource = featureSource
        };
        
        // 실제 로드된 피처 수로 업데이트
        var actualFeatureCount = loadedCount > 0 ? loadedCount : featureCount;
        
        return new LayerInfo
        {
            Id = Guid.NewGuid(),
            Name = layerName,
            FilePath = gdbPath,
            GeometryType = geometryType,
            FeatureCount = actualFeatureCount,
            Extent = extent,
            CRS = $"EPSG:{srid}",
            Layer = layerAdapter
        };
    }
    
    /// <summary>
    /// FileGDB의 모든 레이어 정보를 가져옵니다.
    /// </summary>
    public List<GdbLayerInfo> GetLayersInfo(string gdbPath)
    {
        if (!EnsureGdal())
            throw new InvalidOperationException(_gdalError ?? "GDAL 초기화 실패");
        
        if (!Directory.Exists(gdbPath))
            throw new DirectoryNotFoundException($"GDB 폴더를 찾을 수 없습니다: {gdbPath}");
        
        var results = new List<GdbLayerInfo>();
        
        using var dataset = Ogr.Open(gdbPath, 0);
        if (dataset == null)
            throw new InvalidOperationException($"FileGDB를 열 수 없습니다: {gdbPath}");
        
        var layerCount = dataset.GetLayerCount();
        System.Diagnostics.Debug.WriteLine($"FileGDB 레이어 수: {layerCount}");
        
        for (int i = 0; i < layerCount; i++)
        {
            using var layer = dataset.GetLayerByIndex(i);
            if (layer == null) continue;
            
            var defn = layer.GetLayerDefn();
            var ogrGeomType = defn.GetGeomType();
            var geomType = ConvertGeometryType((int)ogrGeomType);
            var featureCount = (int)layer.GetFeatureCount(1);
            var layerName = layer.GetName() ?? $"Layer_{i}";
            
            System.Diagnostics.Debug.WriteLine($"  레이어 {i}: {layerName}, 지오메트리 타입: {ogrGeomType}, 피처 수: {featureCount}");
            
            results.Add(new GdbLayerInfo
            {
                Index = i,
                Name = layerName,
                FeatureCount = featureCount,
                GeometryType = geomType
            });
        }
        
        return results;
    }
    
    /// <summary>
    /// OGR 지오메트리 타입을 내부 타입으로 변환
    /// </summary>
    private CoreGeometryType ConvertGeometryType(int ogrType)
    {
        var geomType = (wkbGeometryType)ogrType;
        return geomType switch
        {
            // 2D 타입
            wkbGeometryType.wkbPoint => CoreGeometryType.Point,
            wkbGeometryType.wkbLineString => CoreGeometryType.LineString,
            wkbGeometryType.wkbPolygon => CoreGeometryType.Polygon,
            wkbGeometryType.wkbMultiPoint => CoreGeometryType.MultiPoint,
            wkbGeometryType.wkbMultiLineString => CoreGeometryType.MultiLineString,
            wkbGeometryType.wkbMultiPolygon => CoreGeometryType.MultiPolygon,
            wkbGeometryType.wkbGeometryCollection => CoreGeometryType.GeometryCollection,
            
            // Z (3D) 타입 - 25D는 OGR에서 Z를 의미
            wkbGeometryType.wkbPoint25D => CoreGeometryType.PointZ,
            wkbGeometryType.wkbLineString25D => CoreGeometryType.LineStringZ,
            wkbGeometryType.wkbPolygon25D => CoreGeometryType.PolygonZ,
            wkbGeometryType.wkbMultiPoint25D => CoreGeometryType.MultiPointZ,
            wkbGeometryType.wkbMultiLineString25D => CoreGeometryType.MultiLineStringZ,
            wkbGeometryType.wkbMultiPolygon25D => CoreGeometryType.MultiPolygonZ,
            
            // M 타입
            wkbGeometryType.wkbPointM => CoreGeometryType.PointM,
            wkbGeometryType.wkbLineStringM => CoreGeometryType.LineStringM,
            wkbGeometryType.wkbPolygonM => CoreGeometryType.PolygonM,
            wkbGeometryType.wkbMultiPointM => CoreGeometryType.MultiPointM,
            wkbGeometryType.wkbMultiLineStringM => CoreGeometryType.MultiLineStringM,
            wkbGeometryType.wkbMultiPolygonM => CoreGeometryType.MultiPolygonM,
            
            // ZM 타입
            wkbGeometryType.wkbPointZM => CoreGeometryType.PointZM,
            wkbGeometryType.wkbLineStringZM => CoreGeometryType.LineStringZM,
            wkbGeometryType.wkbPolygonZM => CoreGeometryType.PolygonZM,
            wkbGeometryType.wkbMultiPointZM => CoreGeometryType.MultiPointZM,
            wkbGeometryType.wkbMultiLineStringZM => CoreGeometryType.MultiLineStringZM,
            wkbGeometryType.wkbMultiPolygonZM => CoreGeometryType.MultiPolygonZM,
            
            // 비공간 테이블
            wkbGeometryType.wkbNone => CoreGeometryType.None,
            wkbGeometryType.wkbUnknown => CoreGeometryType.Unknown,
            
            _ => CoreGeometryType.Unknown
        };
    }
    
    private static int? TryGetSrid(SpatialReference spatialRef)
    {
        try
        {
            spatialRef.AutoIdentifyEPSG();
            var code = spatialRef.GetAuthorityCode(null);
            if (int.TryParse(code, out var srid))
            {
                return srid;
            }
        }
        catch { }
        return null;
    }
    
    private static EngineEnvelope? ReadExtent(OSGeo.OGR.Layer layer)
    {
        try
        {
            var env = new OSGeo.OGR.Envelope();
            if (layer.GetExtent(env, 1) == 0)
            {
                return new EngineEnvelope(env.MinX, env.MaxX, env.MinY, env.MaxY);
            }
        }
        catch { }
        return null;
    }
    
    private static EngineFeature ConvertFeature(OSGeo.OGR.Feature ogrFeature)
    {
        var attributes = new Engine.Data.AttributeTable();
        var defn = ogrFeature.GetDefnRef();
        
        // FID를 속성에 추가 (ID 조회용)
        var fid = ogrFeature.GetFID();
        attributes["FID"] = fid;
        attributes["OBJECTID"] = fid; // Esri 표준 필드명
        
        for (int i = 0; i < defn.GetFieldCount(); i++)
        {
            var fieldDefn = defn.GetFieldDefn(i);
            var name = fieldDefn.GetName();
            var fieldType = fieldDefn.GetFieldType();
            
            object? value;
            if (fieldType == FieldType.OFTDate || fieldType == FieldType.OFTDateTime)
            {
                ogrFeature.GetFieldAsDateTime(i, out int y, out int m, out int d, out int h, out int min, out float s, out int tz);
                value = y == 0 ? null : new DateTime(y, m == 0 ? 1 : m, d == 0 ? 1 : d, h, min, (int)s, DateTimeKind.Utc);
            }
            else
            {
                value = fieldType switch
                {
                    FieldType.OFTInteger => ogrFeature.GetFieldAsInteger(i),
                    FieldType.OFTInteger64 => ogrFeature.GetFieldAsInteger64(i),
                    FieldType.OFTReal => ogrFeature.GetFieldAsDouble(i),
                    _ => ogrFeature.GetFieldAsString(i)
                };
            }
            
            attributes[name] = value;
        }
        
        // 지오메트리 변환
        var ogrGeometry = ogrFeature.GetGeometryRef();
        SpatialView.Engine.Geometry.IGeometry? engineGeometry = null;
        if (ogrGeometry != null)
        {
            ogrGeometry.ExportToWkt(out string? wkt);
            if (!string.IsNullOrWhiteSpace(wkt))
            {
                try
                {
                    engineGeometry = WktParser.Parse(wkt);
                    
                    // 디버그 로그
                    var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SpatialView_render.log");
                    var envelope = engineGeometry?.Envelope;
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] ConvertFeature: FID={fid}, GeomType={engineGeometry?.GetType().Name}, Envelope={envelope}, WKT(first 200)={wkt.Substring(0, Math.Min(200, wkt.Length))}\n");
                }
                catch (Exception ex)
                {
                    var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SpatialView_render.log");
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] ConvertFeature WKT Parse Error: {ex.Message}, WKT(first 200)={wkt.Substring(0, Math.Min(200, wkt.Length))}\n");
                }
            }
        }
        
        // FID를 ID로 사용하여 피처 생성
        return new EngineFeature(fid, engineGeometry, attributes);
    }
}

/// <summary>
/// GDB 레이어 정보 (레이어 선택 다이얼로그용)
/// </summary>
public class GdbLayerInfo : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isSelected = false;
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public int FeatureCount { get; set; }
    public SpatialView.Core.Enums.GeometryType GeometryType { get; set; }
    
    public bool IsSelected 
    { 
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}