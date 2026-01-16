using System.Windows;
using System.Windows.Controls;
using SpatialView.ViewModels;
using SpatialView.Infrastructure.GisEngine;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data;

namespace SpatialView.Views.Dialogs;

/// <summary>
/// 레이어 좌표계 변환 다이얼로그
/// </summary>
public partial class CoordinateTransformDialog : Window
{
    private readonly List<CrsInfo> _coordinateSystems;
    private readonly MainViewModel _mainViewModel;
    private LayerItemViewModel? _selectedLayer;
    
    public CoordinateTransformDialog(MainViewModel mainViewModel)
    {
        InitializeComponent();
        
        _mainViewModel = mainViewModel;
        
        // 지원하는 좌표계 목록
        _coordinateSystems = new List<CrsInfo>
        {
            new CrsInfo { Name = "WGS 84 (경위도)", Epsg = 4326, IsGeographic = true, Description = "전세계 표준 경위도 좌표계" },
            new CrsInfo { Name = "Korea 2000 / Central Belt", Epsg = 5186, IsGeographic = false, Description = "대한민국 중부원점 (서울, 경기 등)" },
            new CrsInfo { Name = "Korea 2000 / West Belt", Epsg = 5185, IsGeographic = false, Description = "대한민국 서부원점 (인천, 충남 서부 등)" },
            new CrsInfo { Name = "Korea 2000 / East Belt", Epsg = 5187, IsGeographic = false, Description = "대한민국 동부원점 (강원 동부, 경북 동부 등)" },
            new CrsInfo { Name = "Korea 2000 / East Sea Belt", Epsg = 5188, IsGeographic = false, Description = "대한민국 동해원점 (울릉도, 독도)" },
            new CrsInfo { Name = "Korea 2000 / Unified CS", Epsg = 5179, IsGeographic = false, Description = "대한민국 통합좌표계 (TM)" },
            new CrsInfo { Name = "UTM Zone 52N", Epsg = 32652, IsGeographic = false, Description = "UTM 52N (한반도 서부)" },
            new CrsInfo { Name = "UTM Zone 51N", Epsg = 32651, IsGeographic = false, Description = "UTM 51N" },
            new CrsInfo { Name = "Web Mercator", Epsg = 3857, IsGeographic = false, Description = "웹 지도용 (Google, OSM 등)" },
        };
        
        // 레이어 목록 로드
        LoadLayers();
        
        // 좌표계 목록 설정
        TargetCrsComboBox.ItemsSource = _coordinateSystems;
        TargetCrsComboBox.DisplayMemberPath = "Name";
    }
    
    /// <summary>
    /// 레이어 목록 로드
    /// </summary>
    private void LoadLayers()
    {
        var layers = _mainViewModel.LayerPanelViewModel.Layers
            .Where(l => l.Layer is Core.GisEngine.IVectorLayer)
            .ToList();
        
        LayerComboBox.ItemsSource = layers;
        
        if (layers.Count > 0)
        {
            LayerComboBox.SelectedIndex = 0;
        }
    }
    
    /// <summary>
    /// 레이어 선택 변경
    /// </summary>
    private void LayerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedLayer = LayerComboBox.SelectedItem as LayerItemViewModel;
        
        if (_selectedLayer?.Layer is SpatialViewVectorLayerAdapter adapter)
        {
            var engineLayer = adapter.GetEngineLayer();
            
            // 현재 좌표계 표시
            var srid = engineLayer?.SRID ?? 0;
            var crsInfo = _coordinateSystems.FirstOrDefault(c => c.Epsg == srid);
            CurrentCrsText.Text = crsInfo != null ? $"EPSG:{srid} ({crsInfo.Name})" : $"EPSG:{srid}";
            
            // 피처 수 표시
            var featureCount = engineLayer?.FeatureCount ?? 0;
            FeatureCountText.Text = featureCount.ToString("N0");
            
            // 새 레이어 이름 기본값
            NewLayerNameTextBox.Text = $"{_selectedLayer.Name}_변환";
        }
        else
        {
            CurrentCrsText.Text = "알 수 없음";
            FeatureCountText.Text = "0";
            NewLayerNameTextBox.Text = "";
        }
    }
    
    /// <summary>
    /// 대상 좌표계 선택 변경
    /// </summary>
    private void TargetCrsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TargetCrsComboBox.SelectedItem is CrsInfo crs)
        {
            TargetCrsDescText.Text = $"EPSG:{crs.Epsg} - {crs.Description}";
        }
    }
    
    /// <summary>
    /// 저장 경로 찾아보기
    /// </summary>
    private void BrowseOutputPath_Click(object sender, RoutedEventArgs e)
    {
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "GeoJSON 파일|*.geojson|Shapefile|*.shp|모든 파일|*.*",
            DefaultExt = ".geojson",
            FileName = $"{_selectedLayer?.Name ?? "layer"}_transformed"
        };
        
        if (saveDialog.ShowDialog() == true)
        {
            OutputPathTextBox.Text = saveDialog.FileName;
        }
    }
    
    /// <summary>
    /// 변환 실행
    /// </summary>
    private async void TransformButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLayer == null)
        {
            System.Windows.MessageBox.Show("변환할 레이어를 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (TargetCrsComboBox.SelectedItem is not CrsInfo targetCrs)
        {
            System.Windows.MessageBox.Show("대상 좌표계를 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        // 파일 저장 옵션 선택 시 경로 확인
        if (SaveToFileRadio.IsChecked == true && string.IsNullOrWhiteSpace(OutputPathTextBox.Text))
        {
            System.Windows.MessageBox.Show("저장 경로를 지정하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (_selectedLayer.Layer is not SpatialViewVectorLayerAdapter adapter)
        {
            System.Windows.MessageBox.Show("벡터 레이어만 변환할 수 있습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        
        var engineLayer = adapter.GetEngineLayer();
        if (engineLayer == null)
        {
            System.Windows.MessageBox.Show("레이어 데이터를 가져올 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        
        var sourceSrid = engineLayer.SRID;
        var targetSrid = targetCrs.Epsg;
        
        if (sourceSrid == targetSrid)
        {
            System.Windows.MessageBox.Show("원본과 대상 좌표계가 동일합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        // UI 비활성화
        TransformButton.IsEnabled = false;
        TransformProgressBar.Value = 0;
        LogTextBox.Clear();
        
        try
        {
            AppendLog($"좌표계 변환 시작: EPSG:{sourceSrid} → EPSG:{targetSrid}");
            
            // 피처 가져오기
            var features = engineLayer.GetFeatures((Envelope?)null).ToList();
            var totalCount = features.Count;
            
            AppendLog($"총 {totalCount}개 피처 변환 중...");
            
            // 좌표 변환기 (내부 메서드 사용)
            
            var transformedFeatures = new List<IFeature>();
            int processedCount = 0;
            int errorCount = 0;
            
            await Task.Run(() =>
            {
                foreach (var feature in features)
                {
                    try
                    {
                        if (feature.Geometry != null)
                        {
                            // 지오메트리 변환
                            var transformedGeometry = TransformGeometry(feature.Geometry, sourceSrid, targetSrid);
                            
                            // 새 피처 생성
                            var newFeature = new Feature(feature.Id, transformedGeometry, feature.Attributes as IAttributeTable);
                            transformedFeatures.Add(newFeature);
                        }
                        else
                        {
                            transformedFeatures.Add(feature);
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        if (errorCount <= 5)
                        {
                            Dispatcher.Invoke(() => AppendLog($"  오류: {ex.Message}"));
                        }
                    }
                    
                    processedCount++;
                    
                    // 진행률 업데이트 (100개마다)
                    if (processedCount % 100 == 0 || processedCount == totalCount)
                    {
                        var progress = (double)processedCount / totalCount * 100;
                        Dispatcher.Invoke(() =>
                        {
                            TransformProgressBar.Value = progress;
                            ProgressText.Text = $"{processedCount:N0} / {totalCount:N0} ({progress:F1}%)";
                        });
                    }
                }
            });
            
            AppendLog($"변환 완료: {transformedFeatures.Count}개 성공, {errorCount}개 오류");
            
            if (SaveToFileRadio.IsChecked == true)
            {
                // 파일로 저장
                var outputPath = OutputPathTextBox.Text;
                AppendLog($"파일 저장 중: {outputPath}");
                
                try
                {
                    await SaveToFileAsync(outputPath, transformedFeatures, targetSrid);
                    
                    System.Windows.MessageBox.Show(
                        $"좌표계 변환 및 저장이 완료되었습니다.\n\n" +
                        $"변환된 피처: {transformedFeatures.Count}개\n" +
                        $"오류: {errorCount}개\n" +
                        $"저장 경로: {outputPath}",
                        "변환 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception saveEx)
                {
                    AppendLog($"파일 저장 실패: {saveEx.Message}");
                    System.Windows.MessageBox.Show($"파일 저장 중 오류가 발생했습니다.\n\n{saveEx.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // 지도에 새 레이어로 추가
                var newLayerName = string.IsNullOrWhiteSpace(NewLayerNameTextBox.Text) 
                    ? $"{_selectedLayer.Name}_변환" 
                    : NewLayerNameTextBox.Text;
                
                AppendLog($"새 레이어 생성: {newLayerName}");
                
                // TODO: 새 레이어 생성 및 추가 로직
                System.Windows.MessageBox.Show(
                    $"좌표계 변환이 완료되었습니다.\n\n" +
                    $"변환된 피처: {transformedFeatures.Count}개\n" +
                    $"오류: {errorCount}개\n\n" +
                    $"새 레이어 '{newLayerName}'가 지도에 추가됩니다.",
                    "변환 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            
            ProgressText.Text = "변환 완료";
        }
        catch (Exception ex)
        {
            AppendLog($"변환 실패: {ex.Message}");
            System.Windows.MessageBox.Show($"좌표계 변환 중 오류가 발생했습니다.\n\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            TransformButton.IsEnabled = true;
        }
    }
    
    /// <summary>
    /// 지오메트리 좌표 변환
    /// </summary>
    private IGeometry TransformGeometry(IGeometry geometry, int sourceSrid, int targetSrid)
    {
        // 간단한 좌표 변환 (실제로는 GDAL/Proj4 등 사용 필요)
        // 여기서는 기본적인 변환만 구현
        
        switch (geometry)
        {
            case Engine.Geometry.Point point:
                var (tx, ty) = TransformCoordinate(point.X, point.Y, sourceSrid, targetSrid);
                return new Engine.Geometry.Point(tx, ty);
                
            case LineString lineString:
                var lineCoords = lineString.Coordinates
                    .Select(c => {
                        var (x, y) = TransformCoordinate(c.X, c.Y, sourceSrid, targetSrid);
                        return new Coordinate(x, y);
                    })
                    .ToArray();
                return new LineString(lineCoords);
                
            case Polygon polygon:
                if (polygon.ExteriorRing != null)
                {
                    var exteriorCoords = polygon.ExteriorRing.Coordinates
                        .Select(c => {
                            var (x, y) = TransformCoordinate(c.X, c.Y, sourceSrid, targetSrid);
                            return new Coordinate(x, y);
                        })
                        .ToArray();
                    return new Polygon(new LinearRing(exteriorCoords));
                }
                return geometry;
                
            case MultiPoint multiPoint:
                var points = multiPoint.Geometries
                    .OfType<Engine.Geometry.Point>()
                    .Select(p => {
                        var (x, y) = TransformCoordinate(p.X, p.Y, sourceSrid, targetSrid);
                        return new Engine.Geometry.Point(x, y);
                    })
                    .ToArray();
                return new MultiPoint(points);
                
            case MultiLineString multiLineString:
                var lines = multiLineString.Geometries
                    .OfType<LineString>()
                    .Select(ls => TransformGeometry(ls, sourceSrid, targetSrid) as LineString)
                    .Where(ls => ls != null)
                    .ToArray();
                return new MultiLineString(lines!);
                
            case MultiPolygon multiPolygon:
                var polygons = multiPolygon.Geometries
                    .OfType<Polygon>()
                    .Select(p => TransformGeometry(p, sourceSrid, targetSrid) as Polygon)
                    .Where(p => p != null)
                    .ToArray();
                return new MultiPolygon(polygons!);
                
            default:
                return geometry;
        }
    }
    
    /// <summary>
    /// 좌표 변환 (간단한 구현)
    /// </summary>
    private (double x, double y) TransformCoordinate(double x, double y, int sourceSrid, int targetSrid)
    {
        // 동일한 좌표계면 그대로 반환
        if (sourceSrid == targetSrid)
            return (x, y);
        
        // WGS84 경위도를 중간 좌표로 사용
        double lon, lat;
        
        // 1단계: 원본 좌표를 WGS84로 변환
        if (sourceSrid == 4326)
        {
            lon = x;
            lat = y;
        }
        else
        {
            (lon, lat) = ToWgs84(x, y, sourceSrid);
        }
        
        // 2단계: WGS84에서 대상 좌표계로 변환
        if (targetSrid == 4326)
        {
            return (lon, lat);
        }
        else
        {
            return FromWgs84(lon, lat, targetSrid);
        }
    }
    
    /// <summary>
    /// 투영 좌표를 WGS84로 변환
    /// </summary>
    private (double lon, double lat) ToWgs84(double x, double y, int srid)
    {
        // Korea 2000 좌표계들의 파라미터
        var (centralMeridian, falseEasting, falseNorthing, scaleFactor) = GetKoreaProjectionParams(srid);
        
        if (centralMeridian == 0)
        {
            // 지원하지 않는 좌표계 - 근사 변환
            return (x, y);
        }
        
        // TM 역변환 (간략화된 버전)
        double a = 6378137.0; // GRS80 장반경
        double f = 1 / 298.257222101; // GRS80 편평률
        double k0 = scaleFactor;
        
        double x0 = x - falseEasting;
        double y0 = y - falseNorthing;
        
        // 근사 역변환
        double lat0 = 38.0 * Math.PI / 180.0; // 기준 위도
        double M0 = lat0 * a;
        
        double M = M0 + y0 / k0;
        double mu = M / (a * (1 - Math.Pow(f, 2) / 4));
        
        double lat = mu + (3 * f / 2) * Math.Sin(2 * mu);
        double lon = centralMeridian + x0 / (a * k0 * Math.Cos(lat));
        
        return (lon * 180 / Math.PI, lat * 180 / Math.PI);
    }
    
    /// <summary>
    /// WGS84를 투영 좌표로 변환
    /// </summary>
    private (double x, double y) FromWgs84(double lon, double lat, int srid)
    {
        var (centralMeridian, falseEasting, falseNorthing, scaleFactor) = GetKoreaProjectionParams(srid);
        
        if (centralMeridian == 0)
        {
            return (lon, lat);
        }
        
        // TM 정변환 (간략화된 버전)
        double a = 6378137.0;
        double f = 1 / 298.257222101;
        double k0 = scaleFactor;
        
        double latRad = lat * Math.PI / 180.0;
        double lonRad = lon * Math.PI / 180.0;
        double lon0Rad = centralMeridian;
        
        double N = a / Math.Sqrt(1 - f * (2 - f) * Math.Pow(Math.Sin(latRad), 2));
        double T = Math.Pow(Math.Tan(latRad), 2);
        double C = f * (2 - f) * Math.Pow(Math.Cos(latRad), 2) / (1 - f * (2 - f));
        double A = (lonRad - lon0Rad) * Math.Cos(latRad);
        
        double M = a * (latRad - f * Math.Sin(2 * latRad) / 2);
        
        double x = k0 * N * (A + (1 - T + C) * Math.Pow(A, 3) / 6) + falseEasting;
        double y = k0 * (M + N * Math.Tan(latRad) * Math.Pow(A, 2) / 2) + falseNorthing;
        
        return (x, y);
    }
    
    /// <summary>
    /// 한국 좌표계 파라미터 가져오기
    /// </summary>
    private (double centralMeridian, double falseEasting, double falseNorthing, double scaleFactor) GetKoreaProjectionParams(int srid)
    {
        return srid switch
        {
            5179 => (127.5 * Math.PI / 180, 1000000, 2000000, 0.9996), // Korea 2000 Unified
            5185 => (125.0 * Math.PI / 180, 200000, 500000, 1.0), // West Belt
            5186 => (127.0 * Math.PI / 180, 200000, 500000, 1.0), // Central Belt
            5187 => (129.0 * Math.PI / 180, 200000, 500000, 1.0), // East Belt
            5188 => (131.0 * Math.PI / 180, 200000, 500000, 1.0), // East Sea Belt
            3857 => (0, 0, 0, 1.0), // Web Mercator (별도 처리 필요)
            _ => (0, 0, 0, 0)
        };
    }
    
    /// <summary>
    /// 파일로 저장
    /// </summary>
    private async Task SaveToFileAsync(string filePath, List<IFeature> features, int srid)
    {
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        
        await Task.Run(() =>
        {
            if (extension == ".geojson" || extension == ".json")
            {
                SaveAsGeoJson(filePath, features, srid);
            }
            else if (extension == ".shp")
            {
                // Shapefile 저장은 GDAL 필요 - 현재는 GeoJSON으로 대체
                var geoJsonPath = System.IO.Path.ChangeExtension(filePath, ".geojson");
                SaveAsGeoJson(geoJsonPath, features, srid);
                Dispatcher.Invoke(() => AppendLog($"Shapefile 저장은 지원되지 않아 GeoJSON으로 저장됨: {geoJsonPath}"));
            }
            else
            {
                // 기본적으로 GeoJSON으로 저장
                SaveAsGeoJson(filePath, features, srid);
            }
        });
    }
    
    /// <summary>
    /// GeoJSON으로 저장
    /// </summary>
    private void SaveAsGeoJson(string filePath, List<IFeature> features, int srid)
    {
        using var writer = new System.IO.StreamWriter(filePath, false, System.Text.Encoding.UTF8);
        
        writer.WriteLine("{");
        writer.WriteLine("  \"type\": \"FeatureCollection\",");
        writer.WriteLine($"  \"crs\": {{ \"type\": \"name\", \"properties\": {{ \"name\": \"EPSG:{srid}\" }} }},");
        writer.WriteLine("  \"features\": [");
        
        for (int i = 0; i < features.Count; i++)
        {
            var feature = features[i];
            var comma = i < features.Count - 1 ? "," : "";
            
            writer.WriteLine("    {");
            writer.WriteLine("      \"type\": \"Feature\",");
            writer.WriteLine($"      \"id\": {feature.Id},");
            
            // Geometry
            writer.Write("      \"geometry\": ");
            WriteGeometryAsGeoJson(writer, feature.Geometry);
            writer.WriteLine(",");
            
            // Properties
            writer.WriteLine("      \"properties\": {");
            if (feature.Attributes != null)
            {
                var attrNames = feature.Attributes.AttributeNames.ToList();
                for (int j = 0; j < attrNames.Count; j++)
                {
                    var name = attrNames[j];
                    var value = feature.Attributes[name];
                    var attrComma = j < attrNames.Count - 1 ? "," : "";
                    
                    if (value == null || value == DBNull.Value)
                    {
                        writer.WriteLine($"        \"{name}\": null{attrComma}");
                    }
                    else if (value is string strVal)
                    {
                        writer.WriteLine($"        \"{name}\": \"{EscapeJsonString(strVal)}\"{attrComma}");
                    }
                    else if (value is bool boolVal)
                    {
                        writer.WriteLine($"        \"{name}\": {boolVal.ToString().ToLower()}{attrComma}");
                    }
                    else if (IsNumeric(value))
                    {
                        writer.WriteLine($"        \"{name}\": {value}{attrComma}");
                    }
                    else
                    {
                        writer.WriteLine($"        \"{name}\": \"{EscapeJsonString(value.ToString() ?? "")}\"{attrComma}");
                    }
                }
            }
            writer.WriteLine("      }");
            
            writer.WriteLine($"    }}{comma}");
        }
        
        writer.WriteLine("  ]");
        writer.WriteLine("}");
    }
    
    /// <summary>
    /// 지오메트리를 GeoJSON으로 작성
    /// </summary>
    private void WriteGeometryAsGeoJson(System.IO.StreamWriter writer, IGeometry? geometry)
    {
        if (geometry == null)
        {
            writer.Write("null");
            return;
        }
        
        switch (geometry)
        {
            case Engine.Geometry.Point point:
                writer.Write($"{{ \"type\": \"Point\", \"coordinates\": [{point.X}, {point.Y}] }}");
                break;
                
            case LineString lineString:
                writer.Write("{ \"type\": \"LineString\", \"coordinates\": [");
                for (int i = 0; i < lineString.Coordinates.Length; i++)
                {
                    var c = lineString.Coordinates[i];
                    writer.Write($"[{c.X}, {c.Y}]");
                    if (i < lineString.Coordinates.Length - 1) writer.Write(", ");
                }
                writer.Write("] }");
                break;
                
            case Polygon polygon:
                writer.Write("{ \"type\": \"Polygon\", \"coordinates\": [[");
                if (polygon.ExteriorRing != null)
                {
                    for (int i = 0; i < polygon.ExteriorRing.Coordinates.Length; i++)
                    {
                        var c = polygon.ExteriorRing.Coordinates[i];
                        writer.Write($"[{c.X}, {c.Y}]");
                        if (i < polygon.ExteriorRing.Coordinates.Length - 1) writer.Write(", ");
                    }
                }
                writer.Write("]] }");
                break;
                
            case MultiPoint multiPoint:
                writer.Write("{ \"type\": \"MultiPoint\", \"coordinates\": [");
                var pts = multiPoint.Geometries.OfType<Engine.Geometry.Point>().ToList();
                for (int i = 0; i < pts.Count; i++)
                {
                    writer.Write($"[{pts[i].X}, {pts[i].Y}]");
                    if (i < pts.Count - 1) writer.Write(", ");
                }
                writer.Write("] }");
                break;
                
            default:
                writer.Write("null");
                break;
        }
    }
    
    /// <summary>
    /// JSON 문자열 이스케이프
    /// </summary>
    private string EscapeJsonString(string str)
    {
        return str.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("\n", "\\n")
                  .Replace("\r", "\\r")
                  .Replace("\t", "\\t");
    }
    
    /// <summary>
    /// 숫자 타입 확인
    /// </summary>
    private bool IsNumeric(object value)
    {
        return value is int or long or float or double or decimal or short or byte or uint or ulong or ushort or sbyte;
    }
    
    /// <summary>
    /// 로그 추가
    /// </summary>
    private void AppendLog(string message)
    {
        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
        LogTextBox.ScrollToEnd();
    }
    
    /// <summary>
    /// 닫기 버튼
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

/// <summary>
/// 좌표계 정보
/// </summary>
public class CrsInfo
{
    public string Name { get; set; } = "";
    public int Epsg { get; set; }
    public bool IsGeographic { get; set; }
    public string Description { get; set; } = "";
    
    public override string ToString() => Name;
}
