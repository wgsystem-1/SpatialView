using SpatialView.Core.Factories;
using SpatialView.Core.GisEngine;
using SpatialView.Infrastructure.GisEngine;

namespace SpatialView.Infrastructure.Factories;

/// <summary>
/// SpatialView 독립 레이어 생성 팩토리 구현 (SharpMap 제거됨)
/// </summary>
public class LayerFactory : ILayerFactory
{
    public IVectorLayer CreateVectorLayer(string name)
    {
        // SpatialView.Engine의 VectorLayer를 직접 사용
        var engineLayer = new Engine.Data.Layers.VectorLayer();
        engineLayer.Name = name;
        engineLayer.Enabled = true;
        
        // Core IVectorLayer 어댑터로 래핑 (향후 구현)
        return new SpatialViewVectorLayerAdapter(engineLayer);
    }
    
    public IVectorLayer CreateVectorLayer(string name, IDataProvider provider)
    {
        var layer = CreateVectorLayer(name);
        
        // 데이터 소스 연결 (향후 구현)
        // TODO: IDataProvider를 SpatialView.Engine.Data.Sources.IDataSource로 변환
        
        return layer;
    }
    
    public ITileLayer CreateTileLayer(string name, ITileSource tileSource)
    {
        // SpatialView 독립 타일 레이어 (향후 구현)
        throw new NotSupportedException("SpatialView 독립 타일 레이어는 아직 구현되지 않았습니다");
    }
    
    public IVectorLayer CreateHighlightLayer(string name = "Highlight")
    {
        var layer = CreateVectorLayer(name);
        
        // 하이라이트용 기본 스타일 설정
        var style = new StyleFactory().CreateHighlightStyle();
        layer.Style = style;
        
        // 하이라이트 레이어 속성 설정
        layer.MinimumZoom = 0;
        layer.MaximumZoom = double.MaxValue;
        layer.Enabled = true;
        layer.Visible = true;
        
        return layer;
    }
}