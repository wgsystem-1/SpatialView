namespace SpatialView.Engine.Data;

/// <summary>
/// 기본 피처 구현 클래스
/// </summary>
public class Feature : IFeature
{
    /// <inheritdoc/>
    public object Id { get; set; }
    
    /// <inheritdoc/>
    public Geometry.IGeometry? Geometry { get; set; }
    
    /// <inheritdoc/>
    public IAttributeTable Attributes { get; }
    
    /// <inheritdoc/>
    public bool IsValid => Geometry?.IsValid ?? true;
    
    /// <inheritdoc/>
    public Geometry.Envelope? BoundingBox => Geometry?.Envelope;
    
    /// <inheritdoc/>
    public object? GetAttribute(string name)
    {
        return Attributes[name];
    }
    
    /// <inheritdoc/>
    public Styling.IStyle? Style { get; set; }
    
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public Feature()
    {
        Id = Guid.NewGuid();
        Attributes = new AttributeTable();
    }
    
    /// <summary>
    /// ID를 지정하는 생성자
    /// </summary>
    public Feature(object id) : this()
    {
        Id = id;
    }
    
    /// <summary>
    /// 지오메트리와 속성을 지정하는 생성자
    /// </summary>
    public Feature(Geometry.IGeometry? geometry, IAttributeTable? attributes = null)
    {
        Id = Guid.NewGuid();
        Geometry = geometry;
        Attributes = attributes ?? new AttributeTable();
    }
    
    /// <summary>
    /// 전체 정보를 지정하는 생성자
    /// </summary>
    public Feature(object id, Geometry.IGeometry? geometry, IAttributeTable? attributes = null)
    {
        Id = id;
        Geometry = geometry;
        Attributes = attributes ?? new AttributeTable();
    }
    
    /// <summary>
    /// 피처 복사
    /// </summary>
    public Feature Copy()
    {
        var newAttributes = new AttributeTable();
        foreach (var attr in Attributes)
        {
            newAttributes.Add(attr.Key, attr.Value);
        }
        
        return new Feature(Id, Geometry?.Copy(), newAttributes);
    }
    
    /// <summary>
    /// 다른 피처와의 거리 계산
    /// </summary>
    public double Distance(IFeature other)
    {
        if (Geometry == null || other.Geometry == null)
        {
            return double.MaxValue;
        }
        
        return Geometry.Distance(other.Geometry);
    }
    
    /// <summary>
    /// 지오메트리 변환 적용
    /// </summary>
    public void Transform(CoordinateSystems.Transformation.ICoordinateTransformation transformation)
    {
        if (Geometry != null)
        {
            Geometry = transformation.Transform(Geometry);
        }
    }
    
    public override string ToString()
    {
        return $"Feature[Id={Id}, Geometry={Geometry?.GeometryType}, Attributes={Attributes.Count}]";
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is Feature other)
        {
            return Id.Equals(other.Id);
        }
        return false;
    }
    
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}