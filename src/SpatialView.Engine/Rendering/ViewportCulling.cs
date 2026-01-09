using System.Windows;

namespace SpatialView.Engine.Rendering;

/// <summary>
/// 뷰포트 컬링 시스템
/// 보이지 않는 지오메트리를 렌더링에서 제외하여 성능을 향상시킵니다
/// </summary>
public static class ViewportCulling
{
    /// <summary>
    /// 지오메트리가 뷰포트와 교차하는지 확인
    /// </summary>
    /// <param name="geometry">확인할 지오메트리</param>
    /// <param name="viewExtent">뷰포트 영역</param>
    /// <param name="buffer">추가 여유 영역 (화면 단위)</param>
    /// <returns>교차 여부</returns>
    public static bool IsGeometryVisible(Geometry.IGeometry geometry, Geometry.Envelope viewExtent, double buffer = 0)
    {
        if (geometry?.Envelope == null) return false;
        
        var geomExtent = geometry.Envelope;
        
        // 버퍼 적용
        if (buffer > 0)
        {
            var bufferedExtent = new Geometry.Envelope(
                viewExtent.MinX - buffer, viewExtent.MaxX + buffer,
                viewExtent.MinY - buffer, viewExtent.MaxY + buffer);
            return bufferedExtent.Intersects(geomExtent);
        }
        
        return viewExtent.Intersects(geomExtent);
    }
    
    /// <summary>
    /// 포인트가 뷰포트에 보이는지 확인 (심볼 크기 고려)
    /// </summary>
    /// <param name="point">포인트 좌표</param>
    /// <param name="viewExtent">뷰포트 영역</param>
    /// <param name="symbolSize">심볼 크기 (맵 단위)</param>
    /// <returns>보임 여부</returns>
    public static bool IsPointVisible(Geometry.ICoordinate point, Geometry.Envelope viewExtent, double symbolSize = 0)
    {
        if (point == null) return false;
        
        var halfSize = symbolSize / 2;
        var pointExtent = new Geometry.Envelope(
            point.X - halfSize, point.X + halfSize,
            point.Y - halfSize, point.Y + halfSize);
            
        return viewExtent.Intersects(pointExtent);
    }
    
    /// <summary>
    /// 라인이 뷰포트와 교차하는지 확인
    /// </summary>
    /// <param name="lineString">라인 지오메트리</param>
    /// <param name="viewExtent">뷰포트 영역</param>
    /// <param name="lineWidth">라인 두께 (맵 단위)</param>
    /// <returns>교차 여부</returns>
    public static bool IsLineVisible(Geometry.LineString lineString, Geometry.Envelope viewExtent, double lineWidth = 0)
    {
        if (lineString?.Coordinates == null || lineString.Coordinates.Length < 2)
            return false;
            
        // 라인의 경계 영역 확인
        if (lineWidth > 0)
        {
            var bufferedExtent = new Geometry.Envelope(
                viewExtent.MinX - lineWidth, viewExtent.MaxX + lineWidth,
                viewExtent.MinY - lineWidth, viewExtent.MaxY + lineWidth);
            return bufferedExtent.Intersects(lineString.Envelope);
        }
        
        return viewExtent.Intersects(lineString.Envelope);
    }
    
    /// <summary>
    /// 폴리곤이 뷰포트와 교차하는지 확인
    /// </summary>
    /// <param name="polygon">폴리곤 지오메트리</param>
    /// <param name="viewExtent">뷰포트 영역</param>
    /// <returns>교차 여부</returns>
    public static bool IsPolygonVisible(Geometry.Polygon polygon, Geometry.Envelope viewExtent)
    {
        if (polygon?.ExteriorRing?.Coordinates == null)
            return false;
            
        return viewExtent.Intersects(polygon.Envelope);
    }
    
    /// <summary>
    /// 화면 좌표계에서의 컬링 (픽셀 기준)
    /// </summary>
    /// <param name="screenPoints">화면 좌표 배열</param>
    /// <param name="screenSize">화면 크기</param>
    /// <param name="margin">여유 공간 (픽셀)</param>
    /// <returns>화면에 보이는지 여부</returns>
    public static bool IsScreenGeometryVisible(Point[] screenPoints, Size screenSize, double margin = 50)
    {
        if (screenPoints == null || screenPoints.Length == 0) return false;
        
        var screenRect = new Rect(-margin, -margin, 
                                  screenSize.Width + 2 * margin, 
                                  screenSize.Height + 2 * margin);
        
        // 점 중 하나라도 화면 영역에 있으면 보임
        foreach (var point in screenPoints)
        {
            if (screenRect.Contains(point))
                return true;
        }
        
        // 지오메트리가 화면을 가로지르는 경우 체크
        if (screenPoints.Length > 1)
        {
            return DoesGeometryCrossScreen(screenPoints, screenRect);
        }
        
        return false;
    }
    
    /// <summary>
    /// 지오메트리가 화면을 가로지르는지 확인
    /// </summary>
    private static bool DoesGeometryCrossScreen(Point[] points, Rect screenRect)
    {
        // 간단한 경계 박스 체크
        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);
        
        var geomRect = new Rect(minX, minY, maxX - minX, maxY - minY);
        
        return screenRect.IntersectsWith(geomRect);
    }
    
    /// <summary>
    /// 지오메트리 컬렉션의 보이는 지오메트리만 필터링
    /// </summary>
    /// <param name="geometries">지오메트리 컬렉션</param>
    /// <param name="viewExtent">뷰포트 영역</param>
    /// <returns>보이는 지오메트리들</returns>
    public static IEnumerable<Geometry.IGeometry> FilterVisibleGeometries(
        IEnumerable<Geometry.IGeometry> geometries, 
        Geometry.Envelope viewExtent)
    {
        if (geometries == null) yield break;
        
        foreach (var geometry in geometries)
        {
            if (IsGeometryVisible(geometry, viewExtent))
            {
                yield return geometry;
            }
        }
    }
    
    /// <summary>
    /// 피처 컬렉션의 보이는 피처만 필터링
    /// </summary>
    /// <param name="features">피처 컬렉션</param>
    /// <param name="viewExtent">뷰포트 영역</param>
    /// <returns>보이는 피처들</returns>
    public static IEnumerable<Data.IFeature> FilterVisibleFeatures(
        IEnumerable<Data.IFeature> features,
        Geometry.Envelope viewExtent)
    {
        if (features == null) yield break;
        
        foreach (var feature in features)
        {
            if (feature?.Geometry != null && IsGeometryVisible(feature.Geometry, viewExtent))
            {
                yield return feature;
            }
        }
    }
}

/// <summary>
/// Level of Detail (LOD) 시스템
/// 줌 레벨에 따라 렌더링 세부 사항을 조절합니다
/// </summary>
public static class LevelOfDetail
{
    /// <summary>
    /// LOD 레벨 정의
    /// </summary>
    public enum LODLevel
    {
        /// <summary>
        /// 최고 세부 사항 (가장 느림)
        /// </summary>
        Highest = 0,
        
        /// <summary>
        /// 높은 세부 사항
        /// </summary>
        High = 1,
        
        /// <summary>
        /// 중간 세부 사항
        /// </summary>
        Medium = 2,
        
        /// <summary>
        /// 낮은 세부 사항
        /// </summary>
        Low = 3,
        
        /// <summary>
        /// 최소 세부 사항 (가장 빠름)
        /// </summary>
        Lowest = 4
    }
    
    /// <summary>
    /// 줌 레벨에 따른 LOD 레벨 계산
    /// </summary>
    /// <param name="zoom">현재 줌 레벨</param>
    /// <param name="maxZoom">최대 줌 레벨</param>
    /// <returns>LOD 레벨</returns>
    public static LODLevel CalculateLODLevel(double zoom, double maxZoom = 20)
    {
        var normalizedZoom = zoom / maxZoom;
        
        return normalizedZoom switch
        {
            >= 0.8 => LODLevel.Highest,
            >= 0.6 => LODLevel.High,
            >= 0.4 => LODLevel.Medium,
            >= 0.2 => LODLevel.Low,
            _ => LODLevel.Lowest
        };
    }
    
    /// <summary>
    /// LOD에 따른 지오메트리 단순화 허용치 계산
    /// </summary>
    /// <param name="lodLevel">LOD 레벨</param>
    /// <param name="resolution">현재 해상도 (맵 단위/픽셀)</param>
    /// <returns>단순화 허용치</returns>
    public static double GetSimplificationTolerance(LODLevel lodLevel, double resolution)
    {
        var baseTolerances = new[]
        {
            0.0,      // Highest - 단순화 없음
            0.5,      // High
            1.0,      // Medium
            2.0,      // Low
            4.0       // Lowest
        };
        
        return baseTolerances[(int)lodLevel] * resolution;
    }
    
    /// <summary>
    /// LOD에 따른 최소 피처 크기 계산 (픽셀)
    /// </summary>
    /// <param name="lodLevel">LOD 레벨</param>
    /// <returns>최소 피처 크기 (픽셀)</returns>
    public static double GetMinimumFeatureSize(LODLevel lodLevel)
    {
        return lodLevel switch
        {
            LODLevel.Highest => 0.5,
            LODLevel.High => 1.0,
            LODLevel.Medium => 2.0,
            LODLevel.Low => 3.0,
            LODLevel.Lowest => 5.0,
            _ => 1.0
        };
    }
    
    /// <summary>
    /// LOD에 따른 텍스트 렌더링 여부 결정
    /// </summary>
    /// <param name="lodLevel">LOD 레벨</param>
    /// <param name="textSize">텍스트 크기 (픽셀)</param>
    /// <returns>텍스트 렌더링 여부</returns>
    public static bool ShouldRenderText(LODLevel lodLevel, double textSize)
    {
        var minTextSizes = new[]
        {
            0.0,   // Highest - 모든 텍스트
            6.0,   // High
            8.0,   // Medium
            10.0,  // Low
            12.0   // Lowest
        };
        
        return textSize >= minTextSizes[(int)lodLevel];
    }
    
    /// <summary>
    /// LOD에 따른 심볼 렌더링 여부 결정
    /// </summary>
    /// <param name="lodLevel">LOD 레벨</param>
    /// <param name="symbolSize">심볼 크기 (픽셀)</param>
    /// <returns>심볼 렌더링 여부</returns>
    public static bool ShouldRenderSymbol(LODLevel lodLevel, double symbolSize)
    {
        var minSymbolSizes = new[]
        {
            0.0,  // Highest - 모든 심볼
            1.0,  // High
            2.0,  // Medium
            3.0,  // Low
            4.0   // Lowest
        };
        
        return symbolSize >= minSymbolSizes[(int)lodLevel];
    }
    
    /// <summary>
    /// LOD에 따른 라인 단순화 여부 결정
    /// </summary>
    /// <param name="lodLevel">LOD 레벨</param>
    /// <param name="lineLength">라인 길이 (픽셀)</param>
    /// <param name="pointCount">점 개수</param>
    /// <returns>단순화 여부</returns>
    public static bool ShouldSimplifyLine(LODLevel lodLevel, double lineLength, int pointCount)
    {
        if (pointCount < 3) return false;
        
        var maxPointsPerPixel = lodLevel switch
        {
            LODLevel.Highest => double.MaxValue,
            LODLevel.High => 2.0,
            LODLevel.Medium => 1.0,
            LODLevel.Low => 0.5,
            LODLevel.Lowest => 0.25,
            _ => 1.0
        };
        
        var pointsPerPixel = pointCount / Math.Max(lineLength, 1.0);
        return pointsPerPixel > maxPointsPerPixel;
    }
    
    /// <summary>
    /// 지오메트리 크기가 렌더링하기에 충분한지 확인
    /// </summary>
    /// <param name="geometry">지오메트리</param>
    /// <param name="context">렌더링 컨텍스트</param>
    /// <param name="lodLevel">LOD 레벨</param>
    /// <returns>렌더링 여부</returns>
    public static bool ShouldRenderGeometry(Geometry.IGeometry geometry, RenderContext context, LODLevel lodLevel)
    {
        if (geometry?.Envelope == null) return false;
        
        var minSize = GetMinimumFeatureSize(lodLevel);
        
        // 지오메트리의 화면 크기 계산
        var topLeft = context.MapToScreen(new Geometry.Coordinate(geometry.Envelope.MinX, geometry.Envelope.MaxY));
        var bottomRight = context.MapToScreen(new Geometry.Coordinate(geometry.Envelope.MaxX, geometry.Envelope.MinY));
        
        var width = Math.Abs(bottomRight.X - topLeft.X);
        var height = Math.Abs(bottomRight.Y - topLeft.Y);
        var size = Math.Max(width, height);
        
        return size >= minSize;
    }
}