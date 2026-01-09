using System;

namespace SpatialView.Engine.Geometry;

/// <summary>
/// 모든 Geometry 클래스의 기본 추상 클래스
/// </summary>
public abstract class BaseGeometry : IGeometry
{
    public abstract GeometryType GeometryType { get; }
    public abstract bool IsEmpty { get; }
    public abstract int Dimension { get; }
    public abstract ICoordinate[] Coordinates { get; }
    
    public virtual double? M => null;
    
    // IGeometry implementation
    public virtual int SRID { get; set; } = 0;
    public abstract bool IsValid { get; }
    public abstract int NumPoints { get; }
    public abstract Envelope? Envelope { get; }
    public abstract IGeometry Copy();
    public abstract string ToText();
    public abstract double Area { get; }
    public abstract double Length { get; }
    
    ICoordinate IGeometry.Centroid => Centroid?.Coordinate ?? new Coordinate();
    Envelope IGeometry.Envelope => Envelope!;
    public abstract Point? Centroid { get; }
    
    Envelope IGeometry.GetBounds() => Envelope!;
    
    // Spatial relationship operations
    public abstract IGeometry Buffer(double distance);
    public abstract bool Contains(IGeometry geometry);
    public abstract bool Crosses(IGeometry geometry);
    public abstract IGeometry Difference(IGeometry geometry);
    public abstract bool Disjoint(IGeometry geometry);
    public abstract double Distance(IGeometry geometry);
    public abstract bool Intersects(IGeometry geometry);
    public abstract IGeometry Intersection(IGeometry geometry);
    public abstract bool Overlaps(IGeometry geometry);
    public abstract IGeometry SymmetricDifference(IGeometry geometry);
    public abstract bool Touches(IGeometry geometry);
    public abstract IGeometry Union(IGeometry geometry);
    public abstract bool Within(IGeometry geometry);
    
    // Utility methods
    public abstract IGeometry Clone();
    public abstract IGeometry Transform(object transformation);
    
    // Common implementations
    public virtual bool Equals(IGeometry? other)
    {
        if (other == null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GeometryType != other.GeometryType) return false;
        
        var thisCoords = Coordinates;
        var otherCoords = other.Coordinates;
        
        if (thisCoords.Length != otherCoords.Length) return false;
        
        for (int i = 0; i < thisCoords.Length; i++)
        {
            if (!thisCoords[i].Equals2D(otherCoords[i])) return false;
        }
        
        return true;
    }
    
    public override bool Equals(object? obj)
    {
        return obj is IGeometry geometry && Equals(geometry);
    }
    
    public abstract override int GetHashCode();
    public abstract override string ToString();
}