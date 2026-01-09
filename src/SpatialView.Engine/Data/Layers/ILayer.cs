namespace SpatialView.Engine.Data.Layers;

/// <summary>
/// 레이어 인터페이스
/// 공간 데이터의 논리적 그룹
/// </summary>
public interface ILayer
{
    /// <summary>
    /// 레이어 고유 식별자
    /// </summary>
    string Id { get; set; }
    
    /// <summary>
    /// 레이어 이름
    /// </summary>
    string Name { get; set; }
    
    /// <summary>
    /// 레이어 설명
    /// </summary>
    string Description { get; set; }
    
    /// <summary>
    /// 레이어 표시 여부
    /// </summary>
    bool Visible { get; set; }
    
    /// <summary>
    /// 레이어 표시 여부 (호환성 속성)
    /// </summary>
    bool IsVisible { get; }
    
    /// <summary>
    /// 레이어 투명도 (0.0 ~ 1.0)
    /// </summary>
    double Opacity { get; set; }
    
    /// <summary>
    /// 레이어 Z-순서
    /// </summary>
    int ZIndex { get; set; }
    
    /// <summary>
    /// 레이어의 좌표계 (SRID)
    /// </summary>
    int SRID { get; set; }
    
    /// <summary>
    /// 레이어의 전체 영역
    /// </summary>
    Geometry.Envelope? Extent { get; }
    
    /// <summary>
    /// 레이어의 최소 표시 배율
    /// </summary>
    double MinimumZoom { get; set; }
    
    /// <summary>
    /// 레이어의 최대 표시 배율
    /// </summary>
    double MaximumZoom { get; set; }
    
    /// <summary>
    /// 레이어의 최소 표시 축척 (호환성)
    /// </summary>
    double MinScale { get; set; }
    
    /// <summary>
    /// 레이어의 최대 표시 축척 (호환성)
    /// </summary>
    double MaxScale { get; set; }
    
    /// <summary>
    /// 레이어 선택 가능 여부
    /// </summary>
    bool Selectable { get; set; }
    
    /// <summary>
    /// 레이어 선택 가능 여부 (호환성 속성)
    /// </summary>
    bool IsSelectable { get; }
    
    /// <summary>
    /// 레이어 편집 가능 여부
    /// </summary>
    bool Editable { get; set; }
    
    /// <summary>
    /// 레이어 편집 가능 여부 (호환성 속성)
    /// </summary>
    bool IsEditable { get; }
    
    /// <summary>
    /// 레이어 활성화 여부 (호환성 속성, Visible과 동일)
    /// </summary>
    bool Enabled { get; set; }
    
    /// <summary>
    /// 특정 영역의 피처 가져오기
    /// </summary>
    IEnumerable<IFeature> GetFeatures(Geometry.Envelope? extent = null);
    
    /// <summary>
    /// 특정 지오메트리와 교차하는 피처 가져오기
    /// </summary>
    IEnumerable<IFeature> GetFeatures(Geometry.IGeometry geometry);
    
    /// <summary>
    /// 피처 개수
    /// </summary>
    long FeatureCount { get; }
    
    /// <summary>
    /// 피처 추가
    /// </summary>
    void AddFeature(IFeature feature);
    
    /// <summary>
    /// 피처 삭제
    /// </summary>
    void DeleteFeature(IFeature feature);
    
    /// <summary>
    /// 피처 업데이트
    /// </summary>
    void UpdateFeature(IFeature feature);
    
    /// <summary>
    /// 레이어 새로고침
    /// </summary>
    void Refresh();
    
    /// <summary>
    /// 레이어 리소스 해제
    /// </summary>
    void Dispose();
    
    /// <summary>
    /// 레이어 스타일
    /// </summary>
    Styling.IStyle? Style { get; set; }
    
    /// <summary>
    /// 레이어 범위 가져오기
    /// </summary>
    Geometry.Envelope? GetExtent();
    
    /// <summary>
    /// 레이어의 데이터 소스
    /// </summary>
    Sources.IDataSource? DataSource { get; set; }
}