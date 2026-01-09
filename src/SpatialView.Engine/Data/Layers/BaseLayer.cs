using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Data.Layers;

/// <summary>
/// 레이어 기본 구현
/// </summary>
public abstract class BaseLayer : ILayer
{
    protected object _lockObject = new();
    
    public virtual string Id { get; set; } = Guid.NewGuid().ToString();
    
    public virtual string Name { get; set; } = "Layer";
    
    public virtual string Description { get; set; } = string.Empty;
    
    public virtual bool Visible { get; set; } = true;
    
    public virtual bool IsVisible => Visible;
    
    /// <summary>
    /// 레이어 활성화 여부 (호환성 속성)
    /// </summary>
    public virtual bool Enabled { get => Visible; set => Visible = value; }
    
    public virtual double Opacity { get; set; } = 1.0;
    
    public virtual int ZIndex { get; set; }
    
    public virtual int SRID { get; set; } = 4326;
    
    public abstract Envelope? Extent { get; }
    
    public virtual double MinimumZoom { get; set; } = 0;
    
    public virtual double MaximumZoom { get; set; } = double.MaxValue;
    
    public virtual double MinScale { get; set; } = 0;
    
    public virtual double MaxScale { get; set; } = double.MaxValue;
    
    public virtual bool Selectable { get; set; } = true;
    
    public virtual bool IsSelectable => Selectable;
    
    public virtual bool Editable { get; set; } = false;
    
    public virtual bool IsEditable => Editable;
    
    public abstract long FeatureCount { get; }
    
    public abstract IEnumerable<IFeature> GetFeatures(Envelope? extent = null);
    
    public abstract IEnumerable<IFeature> GetFeatures(IGeometry geometry);
    
    public virtual void AddFeature(IFeature feature)
    {
        // 기본 구현은 아무것도 하지 않음
        // 하위 클래스에서 필요에 따라 오버라이드
    }
    
    public virtual void DeleteFeature(IFeature feature)
    {
        // 기본 구현은 아무것도 하지 않음
        // 하위 클래스에서 필요에 따라 오버라이드
    }
    
    public virtual void UpdateFeature(IFeature feature)
    {
        // 기본 구현은 아무것도 하지 않음
        // 하위 클래스에서 필요에 따라 오버라이드
    }
    
    public abstract void Refresh();
    
    public virtual void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose managed resources
        }
    }
    
    public virtual Styling.IStyle? Style { get; set; }
    
    public virtual Sources.IDataSource? DataSource { get; set; }
    
    public virtual Envelope? GetExtent()
    {
        return Extent;
    }
}