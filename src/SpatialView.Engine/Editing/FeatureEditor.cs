using SpatialView.Engine.Data;
using SpatialView.Engine.Data.Layers;
using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Editing;

/// <summary>
/// 피처 편집기 - 피처 생성, 수정, 삭제 기능 제공
/// </summary>
public class FeatureEditor
{
    private readonly VectorLayer _layer;
    private readonly List<EditOperation> _undoStack = new();
    private readonly List<EditOperation> _redoStack = new();
    private const int MaxUndoStackSize = 100;
    
    /// <summary>
    /// 편집 모드
    /// </summary>
    public EditMode CurrentMode { get; set; } = EditMode.None;
    
    /// <summary>
    /// 현재 편집 중인 지오메트리
    /// </summary>
    public IGeometry? CurrentGeometry { get; private set; }
    
    /// <summary>
    /// 현재 편집 중인 피처
    /// </summary>
    public IFeature? CurrentFeature { get; private set; }
    
    /// <summary>
    /// 스냅 허용 오차 (픽셀)
    /// </summary>
    public double SnapTolerance { get; set; } = 10;
    
    /// <summary>
    /// 스냅 활성화
    /// </summary>
    public bool SnapEnabled { get; set; } = true;
    
    /// <summary>
    /// 편집 변경 이벤트
    /// </summary>
    public event Action<EditEventArgs>? EditChanged;
    
    /// <summary>
    /// 편집 완료 이벤트
    /// </summary>
    public event Action<IFeature>? EditCompleted;
    
    /// <summary>
    /// 실행 취소 가능 여부
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;
    
    /// <summary>
    /// 다시 실행 가능 여부
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    public FeatureEditor(VectorLayer layer)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
    }
    
    /// <summary>
    /// 새 피처 생성 시작
    /// </summary>
    public void StartCreate(GeometryType geometryType)
    {
        CurrentMode = EditMode.Create;
        CurrentGeometry = geometryType switch
        {
            GeometryType.Point => new Point(0, 0),
            GeometryType.LineString => new LineString(Array.Empty<Coordinate>()),
            GeometryType.Polygon => new Polygon(new LinearRing(Array.Empty<Coordinate>())),
            _ => null
        };
        
        EditChanged?.Invoke(new EditEventArgs
        {
            Type = EditEventType.CreateStarted,
            Geometry = CurrentGeometry
        });
    }
    
    /// <summary>
    /// 포인트 추가 (LineString, Polygon 생성 시)
    /// </summary>
    public void AddPoint(double x, double y)
    {
        if (CurrentMode != EditMode.Create) return;
        
        var coord = new Coordinate(x, y);
        
        // 스냅 적용
        if (SnapEnabled)
        {
            coord = ApplySnap(coord);
        }
        
        switch (CurrentGeometry)
        {
            case Point point:
                // 포인트는 바로 완료
                CurrentGeometry = new Point(coord);
                CompleteCreate();
                break;
                
            case LineString lineString:
                var lineCoords = lineString.Coordinates.ToList();
                lineCoords.Add(coord);
                CurrentGeometry = new LineString(lineCoords.ToArray());
                break;
                
            case Polygon polygon:
                var ringCoords = polygon.ExteriorRing?.Coordinates.Select(c => c as Coordinate ?? new Coordinate(c.X, c.Y, c.Z)).ToList() ?? new List<Coordinate>();
                // 마지막 점(닫힘 점) 제거 후 추가
                if (ringCoords.Count > 0 && ringCoords[^1].Equals(ringCoords[0]))
                {
                    ringCoords.RemoveAt(ringCoords.Count - 1);
                }
                ringCoords.Add(coord);
                CurrentGeometry = new Polygon(new LinearRing(ringCoords.ToArray()));
                break;
        }
        
        EditChanged?.Invoke(new EditEventArgs
        {
            Type = EditEventType.PointAdded,
            Geometry = CurrentGeometry,
            Point = coord
        });
    }
    
    /// <summary>
    /// 마지막 포인트 제거
    /// </summary>
    public void RemoveLastPoint()
    {
        if (CurrentMode != EditMode.Create) return;
        
        switch (CurrentGeometry)
        {
            case LineString lineString:
                var lineCoords = lineString.Coordinates.ToList();
                if (lineCoords.Count > 0)
                {
                    lineCoords.RemoveAt(lineCoords.Count - 1);
                    CurrentGeometry = new LineString(lineCoords.ToArray());
                }
                break;
                
            case Polygon polygon:
                var ringCoords = polygon.ExteriorRing?.Coordinates.Select(c => c as Coordinate ?? new Coordinate(c.X, c.Y, c.Z)).ToList() ?? new List<Coordinate>();
                if (ringCoords.Count > 0)
                {
                    // 닫힘 점 제거
                    if (ringCoords.Count > 1 && ringCoords[^1].Equals(ringCoords[0]))
                    {
                        ringCoords.RemoveAt(ringCoords.Count - 1);
                    }
                    if (ringCoords.Count > 0)
                    {
                        ringCoords.RemoveAt(ringCoords.Count - 1);
                    }
                    CurrentGeometry = new Polygon(new LinearRing(ringCoords.ToArray()));
                }
                break;
        }
        
        EditChanged?.Invoke(new EditEventArgs
        {
            Type = EditEventType.PointRemoved,
            Geometry = CurrentGeometry
        });
    }
    
    /// <summary>
    /// 피처 생성 완료
    /// </summary>
    public IFeature? CompleteCreate()
    {
        if (CurrentMode != EditMode.Create || CurrentGeometry == null) return null;
        
        // 유효성 검사
        if (!IsValidGeometry(CurrentGeometry))
        {
            EditChanged?.Invoke(new EditEventArgs
            {
                Type = EditEventType.ValidationFailed,
                Geometry = CurrentGeometry,
                ErrorMessage = "유효하지 않은 지오메트리입니다."
            });
            return null;
        }
        
        // 폴리곤 닫기
        if (CurrentGeometry is Polygon polygon)
        {
            var ringCoords = polygon.ExteriorRing?.Coordinates.Select(c => c as Coordinate ?? new Coordinate(c.X, c.Y, c.Z)).ToList() ?? new List<Coordinate>();
            if (ringCoords.Count >= 3)
            {
                if (!ringCoords[^1].Equals(ringCoords[0]))
                {
                    ringCoords.Add(ringCoords[0]);
                }
                CurrentGeometry = new Polygon(new LinearRing(ringCoords.ToArray()));
            }
        }
        
        // 피처 생성
        var feature = new Feature(GenerateFeatureId(), CurrentGeometry, new AttributeTable());
        
        // 레이어에 추가
        AddFeatureToLayer(feature);
        
        // 실행 취소 스택에 추가
        AddToUndoStack(new EditOperation
        {
            Type = EditOperationType.Create,
            Feature = feature
        });
        
        CurrentFeature = feature;
        CurrentMode = EditMode.None;
        
        EditChanged?.Invoke(new EditEventArgs
        {
            Type = EditEventType.CreateCompleted,
            Feature = feature
        });
        
        EditCompleted?.Invoke(feature);
        
        return feature;
    }
    
    /// <summary>
    /// 피처 생성 취소
    /// </summary>
    public void CancelCreate()
    {
        CurrentMode = EditMode.None;
        CurrentGeometry = null;
        
        EditChanged?.Invoke(new EditEventArgs
        {
            Type = EditEventType.CreateCancelled
        });
    }
    
    /// <summary>
    /// 피처 수정 시작
    /// </summary>
    public void StartModify(IFeature feature)
    {
        if (feature?.Geometry == null) return;
        
        CurrentMode = EditMode.Modify;
        CurrentFeature = feature;
        CurrentGeometry = feature.Geometry.Clone();
        
        EditChanged?.Invoke(new EditEventArgs
        {
            Type = EditEventType.ModifyStarted,
            Feature = feature,
            Geometry = CurrentGeometry
        });
    }
    
    /// <summary>
    /// 버텍스 이동
    /// </summary>
    public void MoveVertex(int vertexIndex, double newX, double newY)
    {
        if (CurrentMode != EditMode.Modify || CurrentGeometry == null) return;
        
        var newCoord = new Coordinate(newX, newY);
        
        if (SnapEnabled)
        {
            newCoord = ApplySnap(newCoord);
        }
        
        switch (CurrentGeometry)
        {
            case Point:
                CurrentGeometry = new Point(newCoord);
                break;
                
            case LineString lineString:
                var lineCoords = lineString.Coordinates.ToList();
                if (vertexIndex >= 0 && vertexIndex < lineCoords.Count)
                {
                    lineCoords[vertexIndex] = newCoord;
                    CurrentGeometry = new LineString(lineCoords.ToArray());
                }
                break;
                
            case Polygon polygon:
                var ringCoords = polygon.ExteriorRing?.Coordinates.Select(c => c as Coordinate ?? new Coordinate(c.X, c.Y, c.Z)).ToList() ?? new List<Coordinate>();
                if (vertexIndex >= 0 && vertexIndex < ringCoords.Count)
                {
                    ringCoords[vertexIndex] = newCoord;
                    // 첫 번째/마지막 점 동기화
                    if (vertexIndex == 0)
                    {
                        ringCoords[^1] = newCoord;
                    }
                    else if (vertexIndex == ringCoords.Count - 1)
                    {
                        ringCoords[0] = newCoord;
                    }
                    CurrentGeometry = new Polygon(new LinearRing(ringCoords.ToArray()));
                }
                break;
        }
        
        EditChanged?.Invoke(new EditEventArgs
        {
            Type = EditEventType.VertexMoved,
            Geometry = CurrentGeometry,
            VertexIndex = vertexIndex
        });
    }
    
    /// <summary>
    /// 피처 이동
    /// </summary>
    public void MoveFeature(double deltaX, double deltaY)
    {
        if (CurrentMode != EditMode.Modify || CurrentGeometry == null) return;
        
        CurrentGeometry = TranslateGeometry(CurrentGeometry, deltaX, deltaY);
        
        EditChanged?.Invoke(new EditEventArgs
        {
            Type = EditEventType.FeatureMoved,
            Geometry = CurrentGeometry
        });
    }
    
    /// <summary>
    /// 피처 수정 완료
    /// </summary>
    public void CompleteModify()
    {
        if (CurrentMode != EditMode.Modify || CurrentFeature == null || CurrentGeometry == null) return;
        
        // 이전 지오메트리 저장
        var oldGeometry = CurrentFeature.Geometry?.Clone();
        
        // 지오메트리 업데이트
        CurrentFeature.Geometry = CurrentGeometry;
        
        // 실행 취소 스택에 추가
        AddToUndoStack(new EditOperation
        {
            Type = EditOperationType.Modify,
            Feature = CurrentFeature,
            OldGeometry = oldGeometry,
            NewGeometry = CurrentGeometry
        });
        
        CurrentMode = EditMode.None;
        
        EditChanged?.Invoke(new EditEventArgs
        {
            Type = EditEventType.ModifyCompleted,
            Feature = CurrentFeature
        });
        
        EditCompleted?.Invoke(CurrentFeature);
    }
    
    /// <summary>
    /// 피처 수정 취소
    /// </summary>
    public void CancelModify()
    {
        CurrentMode = EditMode.None;
        CurrentGeometry = null;
        CurrentFeature = null;
        
        EditChanged?.Invoke(new EditEventArgs
        {
            Type = EditEventType.ModifyCancelled
        });
    }
    
    /// <summary>
    /// 피처 삭제
    /// </summary>
    public void DeleteFeature(IFeature feature)
    {
        if (feature == null) return;
        
        // 실행 취소 스택에 추가
        AddToUndoStack(new EditOperation
        {
            Type = EditOperationType.Delete,
            Feature = feature
        });
        
        // 레이어에서 제거
        RemoveFeatureFromLayer(feature);
        
        EditChanged?.Invoke(new EditEventArgs
        {
            Type = EditEventType.FeatureDeleted,
            Feature = feature
        });
    }
    
    /// <summary>
    /// 실행 취소
    /// </summary>
    public void Undo()
    {
        if (!CanUndo) return;
        
        var operation = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        
        switch (operation.Type)
        {
            case EditOperationType.Create:
                // 생성 취소 = 삭제
                if (operation.Feature != null)
                {
                    RemoveFeatureFromLayer(operation.Feature);
                }
                break;
                
            case EditOperationType.Modify:
                // 수정 취소 = 이전 지오메트리로 복원
                if (operation.Feature != null && operation.OldGeometry != null)
                {
                    operation.Feature.Geometry = operation.OldGeometry;
                }
                break;
                
            case EditOperationType.Delete:
                // 삭제 취소 = 복원
                if (operation.Feature != null)
                {
                    AddFeatureToLayer(operation.Feature);
                }
                break;
        }
        
        _redoStack.Add(operation);
        
        EditChanged?.Invoke(new EditEventArgs
        {
            Type = EditEventType.Undone
        });
    }
    
    /// <summary>
    /// 다시 실행
    /// </summary>
    public void Redo()
    {
        if (!CanRedo) return;
        
        var operation = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        
        switch (operation.Type)
        {
            case EditOperationType.Create:
                if (operation.Feature != null)
                {
                    AddFeatureToLayer(operation.Feature);
                }
                break;
                
            case EditOperationType.Modify:
                if (operation.Feature != null && operation.NewGeometry != null)
                {
                    operation.Feature.Geometry = operation.NewGeometry;
                }
                break;
                
            case EditOperationType.Delete:
                if (operation.Feature != null)
                {
                    RemoveFeatureFromLayer(operation.Feature);
                }
                break;
        }
        
        _undoStack.Add(operation);
        
        EditChanged?.Invoke(new EditEventArgs
        {
            Type = EditEventType.Redone
        });
    }
    
    /// <summary>
    /// 스냅 적용
    /// </summary>
    private Coordinate ApplySnap(Coordinate coord)
    {
        // TODO: 다른 피처의 버텍스에 스냅
        // 현재는 그리드 스냅만 구현
        return coord;
    }
    
    /// <summary>
    /// 지오메트리 유효성 검사
    /// </summary>
    private bool IsValidGeometry(IGeometry geometry)
    {
        return geometry switch
        {
            Point => true,
            LineString lineString => lineString.Coordinates.Length >= 2,
            Polygon polygon => polygon.ExteriorRing?.Coordinates.Length >= 4,
            _ => false
        };
    }
    
    /// <summary>
    /// 지오메트리 이동
    /// </summary>
    private IGeometry TranslateGeometry(IGeometry geometry, double deltaX, double deltaY)
    {
        return geometry switch
        {
            Point point => new Point(point.X + deltaX, point.Y + deltaY),
            LineString lineString => new LineString(
                lineString.Coordinates.Select(c => new Coordinate(c.X + deltaX, c.Y + deltaY)).ToArray()),
            Polygon polygon => new Polygon(new LinearRing(
                polygon.ExteriorRing!.Coordinates.Select(c => new Coordinate(c.X + deltaX, c.Y + deltaY)).ToArray())),
            _ => geometry
        };
    }
    
    /// <summary>
    /// 피처 ID 생성
    /// </summary>
    private uint GenerateFeatureId()
    {
        // 간단한 구현 - 실제로는 레이어의 최대 ID + 1 사용
        return (uint)DateTime.Now.Ticks;
    }
    
    /// <summary>
    /// 레이어에 피처 추가
    /// </summary>
    private void AddFeatureToLayer(IFeature feature)
    {
        _layer.AddFeature(feature);
    }
    
    /// <summary>
    /// 레이어에서 피처 제거
    /// </summary>
    private void RemoveFeatureFromLayer(IFeature feature)
    {
        _layer.DeleteFeature(feature);
    }
    
    /// <summary>
    /// 실행 취소 스택에 추가
    /// </summary>
    private void AddToUndoStack(EditOperation operation)
    {
        _undoStack.Add(operation);
        _redoStack.Clear();
        
        // 스택 크기 제한
        while (_undoStack.Count > MaxUndoStackSize)
        {
            _undoStack.RemoveAt(0);
        }
    }
}

/// <summary>
/// 편집 모드
/// </summary>
public enum EditMode
{
    None,
    Create,
    Modify,
    Delete
}

/// <summary>
/// 지오메트리 유형
/// </summary>
public enum GeometryType
{
    Point,
    LineString,
    Polygon
}

/// <summary>
/// 편집 작업 유형
/// </summary>
public enum EditOperationType
{
    Create,
    Modify,
    Delete
}

/// <summary>
/// 편집 작업 (실행 취소/다시 실행용)
/// </summary>
public class EditOperation
{
    public EditOperationType Type { get; set; }
    public IFeature? Feature { get; set; }
    public IGeometry? OldGeometry { get; set; }
    public IGeometry? NewGeometry { get; set; }
}

/// <summary>
/// 편집 이벤트 유형
/// </summary>
public enum EditEventType
{
    CreateStarted,
    PointAdded,
    PointRemoved,
    CreateCompleted,
    CreateCancelled,
    ValidationFailed,
    ModifyStarted,
    VertexMoved,
    FeatureMoved,
    ModifyCompleted,
    ModifyCancelled,
    FeatureDeleted,
    Undone,
    Redone
}

/// <summary>
/// 편집 이벤트 인자
/// </summary>
public class EditEventArgs
{
    public EditEventType Type { get; set; }
    public IFeature? Feature { get; set; }
    public IGeometry? Geometry { get; set; }
    public Coordinate? Point { get; set; }
    public int VertexIndex { get; set; }
    public string? ErrorMessage { get; set; }
}
