using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Data;

/// <summary>
/// 피처들의 컬렉션
/// </summary>
public class FeatureCollection : IEnumerable<IFeature>
{
    private readonly List<IFeature> _features;
    
    /// <summary>
    /// 피처 개수
    /// </summary>
    public int Count => _features.Count;
    
    /// <summary>
    /// 전체 영역
    /// </summary>
    public Envelope? Extent
    {
        get
        {
            if (_features.Count == 0) return null;
            
            Envelope? extent = null;
            foreach (var feature in _features)
            {
                if (feature.BoundingBox != null)
                {
                    if (extent == null)
                        extent = new Envelope(feature.BoundingBox);
                    else
                        extent.ExpandToInclude(feature.BoundingBox);
                }
            }
            
            return extent;
        }
    }
    
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public FeatureCollection()
    {
        _features = new List<IFeature>();
    }
    
    /// <summary>
    /// 피처 목록으로 생성
    /// </summary>
    public FeatureCollection(IEnumerable<IFeature> features)
    {
        _features = new List<IFeature>(features ?? Enumerable.Empty<IFeature>());
    }
    
    /// <summary>
    /// 피처 추가
    /// </summary>
    public void Add(IFeature feature)
    {
        if (feature == null)
            throw new ArgumentNullException(nameof(feature));
        
        _features.Add(feature);
    }
    
    /// <summary>
    /// 피처 제거
    /// </summary>
    public bool Remove(IFeature feature)
    {
        return _features.Remove(feature);
    }
    
    /// <summary>
    /// 모든 피처 제거
    /// </summary>
    public void Clear()
    {
        _features.Clear();
    }
    
    /// <summary>
    /// 인덱스로 피처 접근
    /// </summary>
    public IFeature this[int index]
    {
        get => _features[index];
        set => _features[index] = value ?? throw new ArgumentNullException(nameof(value));
    }
    
    /// <summary>
    /// ID로 피처 찾기
    /// </summary>
    public IFeature? GetFeatureById(object id)
    {
        return _features.FirstOrDefault(f => Equals(f.Id, id));
    }
    
    /// <summary>
    /// 영역 내 피처 찾기
    /// </summary>
    public IEnumerable<IFeature> GetFeaturesInExtent(Envelope extent)
    {
        return _features.Where(f => 
            f.BoundingBox != null && extent.Intersects(f.BoundingBox));
    }
    
    /// <summary>
    /// 속성으로 피처 필터링
    /// </summary>
    public IEnumerable<IFeature> FilterByAttribute(string attributeName, object value)
    {
        return _features.Where(f => 
            f.Attributes != null && 
            f.Attributes.Exists(attributeName) && 
            Equals(f.Attributes[attributeName], value));
    }
    
    /// <summary>
    /// 지오메트리 타입으로 필터링
    /// </summary>
    public IEnumerable<IFeature> FilterByGeometryType(GeometryType geometryType)
    {
        return _features.Where(f => 
            f.Geometry != null && f.Geometry.GeometryType == geometryType);
    }
    
    /// <summary>
    /// 배열로 변환
    /// </summary>
    public IFeature[] ToArray()
    {
        return _features.ToArray();
    }
    
    /// <summary>
    /// 리스트로 변환
    /// </summary>
    public List<IFeature> ToList()
    {
        return new List<IFeature>(_features);
    }
    
    public IEnumerator<IFeature> GetEnumerator()
    {
        return _features.GetEnumerator();
    }
    
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}