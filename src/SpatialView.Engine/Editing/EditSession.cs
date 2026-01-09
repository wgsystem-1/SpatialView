using SpatialView.Engine.Events;
using SpatialView.Engine.Data;
using SpatialView.Engine.Data.Layers;
using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Editing;

/// <summary>
/// 편집 세션 관리자
/// </summary>
public class EditSession : IDisposable
{
    private readonly Stack<IEditOperation> _undoStack = new();
    private readonly Stack<IEditOperation> _redoStack = new();
    private readonly IEventBus _eventBus;
    private readonly Dictionary<string, object> _sessionData = new();
    private bool _isActive;
    private bool _hasChanges;
    private bool _disposed;

    /// <summary>
    /// 세션 ID
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// 세션 시작 시간
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// 세션 활성 상태
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// 변경 사항 존재 여부
    /// </summary>
    public bool HasChanges => _hasChanges;

    /// <summary>
    /// 실행 취소 가능 여부
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// 재실행 가능 여부
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// 최대 실행 취소 횟수
    /// </summary>
    public int MaxUndoCount { get; set; } = 100;

    /// <summary>
    /// 편집 작업 시작 이벤트
    /// </summary>
    public event EventHandler<EditOperationEventArgs>? OperationExecuted;

    /// <summary>
    /// 편집 작업 실행 취소 이벤트
    /// </summary>
    public event EventHandler<EditOperationEventArgs>? OperationUndone;

    /// <summary>
    /// 편집 작업 재실행 이벤트
    /// </summary>
    public event EventHandler<EditOperationEventArgs>? OperationRedone;

    public EditSession(IEventBus eventBus)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        SessionId = Guid.NewGuid().ToString();
        StartTime = DateTime.Now;
    }

    /// <summary>
    /// 편집 세션 시작
    /// </summary>
    public void StartEditing()
    {
        if (_isActive)
            throw new InvalidOperationException("편집 세션이 이미 활성화되어 있습니다.");

        _isActive = true;
        _hasChanges = false;
        
        // 이벤트 구독
        _eventBus.Subscribe<BeforeEditEvent>(OnBeforeEdit);
        _eventBus.Subscribe<AfterEditEvent>(OnAfterEdit);
    }

    /// <summary>
    /// 편집 세션 중지
    /// </summary>
    public void StopEditing(bool saveChanges = true)
    {
        if (!_isActive)
            return;

        if (saveChanges && _hasChanges)
        {
            SaveChanges();
        }
        else if (!saveChanges && _hasChanges)
        {
            DiscardChanges();
        }

        _isActive = false;
        _hasChanges = false;
        
        // 스택 정리
        _undoStack.Clear();
        _redoStack.Clear();
    }

    /// <summary>
    /// 편집 작업 실행
    /// </summary>
    public bool Execute(IEditOperation operation)
    {
        if (!_isActive)
            throw new InvalidOperationException("편집 세션이 활성화되지 않았습니다.");

        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        try
        {
            // 작업 실행
            if (operation.Execute())
            {
                // 실행 취소 스택에 추가
                _undoStack.Push(operation);
                
                // 재실행 스택 초기화
                _redoStack.Clear();
                
                // 스택 크기 제한
                while (_undoStack.Count > MaxUndoCount)
                {
                    var oldestOperation = _undoStack.ToArray().Last();
                    var tempStack = new Stack<IEditOperation>(_undoStack.Where(op => op != oldestOperation).Reverse());
                    _undoStack.Clear();
                    foreach (var op in tempStack)
                    {
                        _undoStack.Push(op);
                    }
                }
                
                _hasChanges = true;
                
                // 이벤트 발생
                OperationExecuted?.Invoke(this, new EditOperationEventArgs(operation));
                
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"편집 작업 실행 실패: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// 실행 취소
    /// </summary>
    public bool Undo()
    {
        if (!CanUndo)
            return false;

        var operation = _undoStack.Pop();
        
        try
        {
            if (operation.Undo())
            {
                _redoStack.Push(operation);
                
                // 이벤트 발생
                OperationUndone?.Invoke(this, new EditOperationEventArgs(operation));
                
                // 변경 사항 확인
                _hasChanges = _undoStack.Count > 0;
                
                return true;
            }
            else
            {
                // 실패 시 다시 넣기
                _undoStack.Push(operation);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"실행 취소 실패: {ex.Message}");
            _undoStack.Push(operation);
        }

        return false;
    }

    /// <summary>
    /// 재실행
    /// </summary>
    public bool Redo()
    {
        if (!CanRedo)
            return false;

        var operation = _redoStack.Pop();
        
        try
        {
            if (operation.Execute())
            {
                _undoStack.Push(operation);
                _hasChanges = true;
                
                // 이벤트 발생
                OperationRedone?.Invoke(this, new EditOperationEventArgs(operation));
                
                return true;
            }
            else
            {
                // 실패 시 다시 넣기
                _redoStack.Push(operation);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"재실행 실패: {ex.Message}");
            _redoStack.Push(operation);
        }

        return false;
    }

    /// <summary>
    /// 실행 취소 스택 정보 가져오기
    /// </summary>
    public IReadOnlyList<string> GetUndoStack()
    {
        return _undoStack.Select(op => op.Description).ToList();
    }

    /// <summary>
    /// 재실행 스택 정보 가져오기
    /// </summary>
    public IReadOnlyList<string> GetRedoStack()
    {
        return _redoStack.Select(op => op.Description).ToList();
    }

    /// <summary>
    /// 세션 데이터 저장
    /// </summary>
    public void SetSessionData(string key, object value)
    {
        _sessionData[key] = value;
    }

    /// <summary>
    /// 세션 데이터 가져오기
    /// </summary>
    public T? GetSessionData<T>(string key)
    {
        return _sessionData.TryGetValue(key, out var value) ? (T)value : default;
    }

    /// <summary>
    /// 변경 사항 저장
    /// </summary>
    private void SaveChanges()
    {
        // 실제 구현에서는 데이터베이스나 파일에 저장
        System.Diagnostics.Debug.WriteLine($"편집 세션 {SessionId}의 변경 사항이 저장되었습니다.");
    }

    /// <summary>
    /// 변경 사항 취소
    /// </summary>
    private void DiscardChanges()
    {
        // 모든 작업 실행 취소
        while (CanUndo)
        {
            Undo();
        }
        
        System.Diagnostics.Debug.WriteLine($"편집 세션 {SessionId}의 변경 사항이 취소되었습니다.");
    }

    private void OnBeforeEdit(BeforeEditEvent e)
    {
        // 편집 전 검증 로직
        if (!_isActive)
        {
            e.Cancel("편집 세션이 활성화되지 않았습니다.");
        }
    }

    private void OnAfterEdit(AfterEditEvent e)
    {
        // 편집 후 처리 로직
        if (e.Success)
        {
            _hasChanges = true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_isActive)
        {
            StopEditing(false);
        }

        _undoStack.Clear();
        _redoStack.Clear();
        _sessionData.Clear();
        
        _disposed = true;
    }
}

/// <summary>
/// 편집 작업 인터페이스
/// </summary>
public interface IEditOperation
{
    /// <summary>
    /// 작업 설명
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 작업 실행
    /// </summary>
    bool Execute();

    /// <summary>
    /// 작업 실행 취소
    /// </summary>
    bool Undo();
}

/// <summary>
/// 편집 작업 이벤트 인수
/// </summary>
public class EditOperationEventArgs : EventArgs
{
    public IEditOperation Operation { get; }
    public DateTime Timestamp { get; }

    public EditOperationEventArgs(IEditOperation operation)
    {
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// 복합 편집 작업
/// </summary>
public class CompositeEditOperation : IEditOperation
{
    private readonly List<IEditOperation> _operations = new();
    
    public string Description { get; }

    public CompositeEditOperation(string description)
    {
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public void AddOperation(IEditOperation operation)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        
        _operations.Add(operation);
    }

    public bool Execute()
    {
        foreach (var operation in _operations)
        {
            if (!operation.Execute())
            {
                // 실패 시 이전 작업들 롤백
                var index = _operations.IndexOf(operation);
                for (int i = index - 1; i >= 0; i--)
                {
                    _operations[i].Undo();
                }
                return false;
            }
        }
        return true;
    }

    public bool Undo()
    {
        // 역순으로 실행 취소
        for (int i = _operations.Count - 1; i >= 0; i--)
        {
            if (!_operations[i].Undo())
            {
                // 실패 시 이전 작업들 다시 실행
                for (int j = i + 1; j < _operations.Count; j++)
                {
                    _operations[j].Execute();
                }
                return false;
            }
        }
        return true;
    }
}

/// <summary>
/// 기본 편집 작업 구현
/// </summary>
public abstract class EditOperationBase : IEditOperation
{
    public abstract string Description { get; }
    
    protected ILayer Layer { get; }
    protected IFeature Feature { get; }

    protected EditOperationBase(ILayer layer, IFeature feature)
    {
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        Feature = feature ?? throw new ArgumentNullException(nameof(feature));
    }

    public abstract bool Execute();
    public abstract bool Undo();
}

/// <summary>
/// 피처 추가 작업
/// </summary>
public class AddFeatureOperation : EditOperationBase
{
    public override string Description => $"피처 추가: {Feature.Id}";

    public AddFeatureOperation(ILayer layer, IFeature feature) : base(layer, feature)
    {
    }

    public override bool Execute()
    {
        Layer.AddFeature(Feature);
        return true;
    }

    public override bool Undo()
    {
        Layer.DeleteFeature(Feature);
        return true;
    }
}

/// <summary>
/// 피처 삭제 작업
/// </summary>
public class DeleteFeatureOperation : EditOperationBase
{
    public override string Description => $"피처 삭제: {Feature.Id}";

    public DeleteFeatureOperation(ILayer layer, IFeature feature) : base(layer, feature)
    {
    }

    public override bool Execute()
    {
        Layer.DeleteFeature(Feature);
        return true;
    }

    public override bool Undo()
    {
        Layer.AddFeature(Feature);
        return true;
    }
}

/// <summary>
/// 피처 수정 작업
/// </summary>
public class ModifyFeatureOperation : IEditOperation
{
    private readonly ILayer _layer;
    private readonly IFeature _originalFeature;
    private readonly IFeature _modifiedFeature;

    public string Description => $"피처 수정: {_originalFeature.Id}";

    public ModifyFeatureOperation(ILayer layer, IFeature originalFeature, IFeature modifiedFeature)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _originalFeature = originalFeature ?? throw new ArgumentNullException(nameof(originalFeature));
        _modifiedFeature = modifiedFeature ?? throw new ArgumentNullException(nameof(modifiedFeature));
    }

    public bool Execute()
    {
        _layer.UpdateFeature(_modifiedFeature);
        return true;
    }

    public bool Undo()
    {
        _layer.UpdateFeature(_originalFeature);
        return true;
    }
}

/// <summary>
/// 도형 이동 작업
/// </summary>
public class MoveGeometryOperation : IEditOperation
{
    private readonly ILayer _layer;
    private readonly IFeature _feature;
    private readonly IGeometry _originalGeometry;
    private readonly IGeometry _newGeometry;

    public string Description => $"도형 이동: {_feature.Id}";

    public MoveGeometryOperation(ILayer layer, IFeature feature, IGeometry originalGeometry, IGeometry newGeometry)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _feature = feature ?? throw new ArgumentNullException(nameof(feature));
        _originalGeometry = originalGeometry ?? throw new ArgumentNullException(nameof(originalGeometry));
        _newGeometry = newGeometry ?? throw new ArgumentNullException(nameof(newGeometry));
    }

    public bool Execute()
    {
        _feature.Geometry = _newGeometry;
        _layer.UpdateFeature(_feature);
        return true;
    }

    public bool Undo()
    {
        _feature.Geometry = _originalGeometry;
        _layer.UpdateFeature(_feature);
        return true;
    }
}

/// <summary>
/// 속성 변경 작업
/// </summary>
public class ModifyAttributeOperation : IEditOperation
{
    private readonly ILayer _layer;
    private readonly IFeature _feature;
    private readonly string _attributeName;
    private readonly object? _oldValue;
    private readonly object? _newValue;

    public string Description => $"속성 변경: {_feature.Id}.{_attributeName}";

    public ModifyAttributeOperation(ILayer layer, IFeature feature, string attributeName, object? oldValue, object? newValue)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _feature = feature ?? throw new ArgumentNullException(nameof(feature));
        _attributeName = attributeName ?? throw new ArgumentNullException(nameof(attributeName));
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public bool Execute()
    {
        _feature.Attributes[_attributeName] = _newValue;
        _layer.UpdateFeature(_feature);
        return true;
    }

    public bool Undo()
    {
        _feature.Attributes[_attributeName] = _oldValue;
        _layer.UpdateFeature(_feature);
        return true;
    }
}