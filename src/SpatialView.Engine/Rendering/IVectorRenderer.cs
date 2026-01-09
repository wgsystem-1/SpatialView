namespace SpatialView.Engine.Rendering;

/// <summary>
/// 벡터 렌더러 인터페이스
/// 지오메트리를 화면에 그리는 렌더러
/// </summary>
public interface IVectorRenderer
{
    /// <summary>
    /// 피처 목록 렌더링
    /// </summary>
    void RenderFeatures(IEnumerable<Data.IFeature> features, RenderContext context);
    
    /// <summary>
    /// 단일 피처 렌더링
    /// </summary>
    void RenderFeature(Data.IFeature feature, RenderContext context);
    
    /// <summary>
    /// 지오메트리 렌더링
    /// </summary>
    void RenderGeometry(Geometry.IGeometry geometry, RenderContext context, Styling.IStyle? style = null);
    
    /// <summary>
    /// 포인트 렌더링
    /// </summary>
    void RenderPoint(Geometry.Point point, RenderContext context, Styling.IPointStyle? style = null);
    
    /// <summary>
    /// 라인 렌더링
    /// </summary>
    void RenderLineString(Geometry.LineString lineString, RenderContext context, Styling.ILineStyle? style = null);
    
    /// <summary>
    /// 폴리곤 렌더링
    /// </summary>
    void RenderPolygon(Geometry.Polygon polygon, RenderContext context, Styling.IPolygonStyle? style = null);
    
    /// <summary>
    /// 멀티 지오메트리 렌더링
    /// </summary>
    void RenderMultiGeometry(Geometry.IGeometry multiGeometry, RenderContext context, Styling.IStyle? style = null);
}