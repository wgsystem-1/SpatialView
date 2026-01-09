using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data;
using SpatialView.Engine.Data.Layers;
using SpatialView.Engine.Analysis;

namespace SpatialView.Engine.Plugins.Tools;

/// <summary>
/// 편집 도구 기본 플러그인
/// </summary>
public abstract class EditingToolPlugin : BaseToolPlugin
{
    protected IFeature? _selectedFeature;
    protected ILayer? _editLayer;
    protected readonly Stack<IEditCommand> _undoStack = new();
    protected readonly Stack<IEditCommand> _redoStack = new();

    public IFeature? SelectedFeature => _selectedFeature;
    public ILayer? EditLayer => _editLayer;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    protected override Task<bool> OnInitializeAsync(IPluginContext context)
    {
        // 피처 선택 이벤트 구독
        SubscribeEvent<Events.FeatureSelectedEvent>(OnFeatureSelected);
        
        return Task.FromResult(true);
    }

    public override bool OnKeyDown(KeyEventArgs e)
    {
        if ((e.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            // 키보드 단축키 처리는 UI 레이어에서 처리하도록 변경
            // Key enum이 정의되지 않았으므로 문자열 비교로 임시 대체
            var keyName = e.Key.ToString();
            switch (keyName)
            {
                case "Z":
                    Undo();
                    e.Handled = true;
                    return true;

                case "Y":
                    Redo();
                    e.Handled = true;
                    return true;
            }
        }

        return false;
    }

    protected virtual void OnFeatureSelected(Events.FeatureSelectedEvent e)
    {
        _selectedFeature = e.Feature;
        _editLayer = e.Layer;
    }

    protected void ExecuteCommand(IEditCommand command)
    {
        if (command.Execute())
        {
            _undoStack.Push(command);
            _redoStack.Clear(); // 새 명령 실행 시 redo 스택 초기화
            
            Log($"명령 실행: {command.Description}");
        }
        else
        {
            Log($"명령 실행 실패: {command.Description}", LogLevel.Error);
        }
    }

    public void Undo()
    {
        if (_undoStack.Count > 0)
        {
            var command = _undoStack.Pop();
            if (command.Undo())
            {
                _redoStack.Push(command);
                Log($"실행 취소: {command.Description}");
            }
            else
            {
                _undoStack.Push(command); // 실패 시 다시 넣기
                Log($"실행 취소 실패: {command.Description}", LogLevel.Error);
            }
        }
    }

    public void Redo()
    {
        if (_redoStack.Count > 0)
        {
            var command = _redoStack.Pop();
            if (command.Execute())
            {
                _undoStack.Push(command);
                Log($"재실행: {command.Description}");
            }
            else
            {
                _redoStack.Push(command); // 실패 시 다시 넣기
                Log($"재실행 실패: {command.Description}", LogLevel.Error);
            }
        }
    }

    protected override void OnDispose()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        base.OnDispose();
    }
}

/// <summary>
/// 편집 명령 인터페이스
/// </summary>
public interface IEditCommand
{
    string Description { get; }
    bool Execute();
    bool Undo();
}

/// <summary>
/// 이동 편집 도구
/// </summary>
public class MoveEditTool : EditingToolPlugin
{
    private bool _isMoving;
    private ICoordinate? _startPosition;
    private IGeometry? _originalGeometry;

    public override string Id => "SpatialView.Tools.EditMove";
    public override string Name => "이동 편집";
    public override string Description => "피처를 이동하는 편집 도구";
    public override Version Version => new Version(1, 0, 0, 0);
    public override string Author => "SpatialView Team";

    public override string ToolName => "이동";
    public override string? ToolIcon => null;
    public override string ToolCategory => "편집";

    protected override void OnActivate()
    {
        Log("이동 편집 도구 활성화");
    }

    protected override void OnDeactivate()
    {
        Log("이동 편집 도구 비활성화");
        CancelMove();
    }

    public override bool OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButton.Left || e.WorldCoordinate == null)
            return false;

        if (!_isMoving && _selectedFeature != null)
        {
            StartMove(e.WorldCoordinate);
            e.Handled = true;
            return true;
        }
        else if (_isMoving)
        {
            CompleteMove(e.WorldCoordinate);
            e.Handled = true;
            return true;
        }

        return false;
    }

    public override bool OnMouseMove(MouseEventArgs e)
    {
        if (_isMoving && e.WorldCoordinate != null && _startPosition != null)
        {
            PreviewMove(e.WorldCoordinate);
            e.Handled = true;
            return true;
        }

        return false;
    }

    public override bool OnKeyDown(KeyEventArgs e)
    {
        if (base.OnKeyDown(e))
            return true;

        if (e.Key == Key.Escape && _isMoving)
        {
            CancelMove();
            e.Handled = true;
            return true;
        }

        return false;
    }

    private void StartMove(ICoordinate position)
    {
        if (_selectedFeature?.Geometry == null || _editLayer == null)
            return;

        _isMoving = true;
        _startPosition = position;
        _originalGeometry = _selectedFeature.Geometry.Clone();
        
        Log("이동 시작");
    }

    private void PreviewMove(ICoordinate currentPosition)
    {
        if (_selectedFeature?.Geometry == null || _startPosition == null)
            return;

        var dx = currentPosition.X - _startPosition.X;
        var dy = currentPosition.Y - _startPosition.Y;

        // 미리보기 표시 (실제로는 임시 렌더링)
        var previewGeometry = TranslateGeometry(_originalGeometry!, dx, dy);
        // TODO: 미리보기 렌더링
    }

    private void CompleteMove(ICoordinate endPosition)
    {
        if (_selectedFeature == null || _editLayer == null || _startPosition == null || _originalGeometry == null)
        {
            CancelMove();
            return;
        }

        var dx = endPosition.X - _startPosition.X;
        var dy = endPosition.Y - _startPosition.Y;

        var newGeometry = TranslateGeometry(_originalGeometry, dx, dy);
        
        var command = new MoveCommand(_selectedFeature, _editLayer, _originalGeometry, newGeometry);
        ExecuteCommand(command);

        _isMoving = false;
        _startPosition = null;
        _originalGeometry = null;
    }

    private void CancelMove()
    {
        _isMoving = false;
        _startPosition = null;
        _originalGeometry = null;
        
        Log("이동 취소");
    }

    private static IGeometry TranslateGeometry(IGeometry geometry, double dx, double dy)
    {
        // 변환 행렬을 사용한 이동
        return geometry.Transform((Func<ICoordinate, ICoordinate>)(coord => new Coordinate(coord.X + dx, coord.Y + dy)));
    }
}

/// <summary>
/// 이동 명령
/// </summary>
public class MoveCommand : IEditCommand
{
    private readonly IFeature _feature;
    private readonly ILayer _layer;
    private readonly IGeometry _oldGeometry;
    private readonly IGeometry _newGeometry;

    public string Description => "피처 이동";

    public MoveCommand(IFeature feature, ILayer layer, IGeometry oldGeometry, IGeometry newGeometry)
    {
        _feature = feature ?? throw new ArgumentNullException(nameof(feature));
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _oldGeometry = oldGeometry ?? throw new ArgumentNullException(nameof(oldGeometry));
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
        _feature.Geometry = _oldGeometry;
        _layer.UpdateFeature(_feature);
        return true;
    }
}

/// <summary>
/// 정점 편집 도구
/// </summary>
public class VertexEditTool : EditingToolPlugin
{
    private readonly List<VertexHandle> _vertexHandles = new();
    private VertexHandle? _selectedVertex;
    private bool _isDragging;

    public override string Id => "SpatialView.Tools.EditVertex";
    public override string Name => "정점 편집";
    public override string Description => "도형의 정점을 편집하는 도구";
    public override Version Version => new Version(1, 0, 0, 0);
    public override string Author => "SpatialView Team";

    public override string ToolName => "정점";
    public override string? ToolIcon => null;
    public override string ToolCategory => "편집";

    protected override void OnActivate()
    {
        Log("정점 편집 도구 활성화");
        UpdateVertexHandles();
    }

    protected override void OnDeactivate()
    {
        Log("정점 편집 도구 비활성화");
        ClearVertexHandles();
    }

    protected override void OnFeatureSelected(Events.FeatureSelectedEvent e)
    {
        base.OnFeatureSelected(e);
        UpdateVertexHandles();
    }

    public override bool OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButton.Left || e.WorldCoordinate == null)
            return false;

        // 정점 선택
        var handle = GetVertexHandleAt(e.WorldCoordinate);
        if (handle != null)
        {
            _selectedVertex = handle;
            _isDragging = true;
            e.Handled = true;
            return true;
        }
        
        // 새 정점 추가 (Ctrl 키)
        if ((e.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            AddVertex(e.WorldCoordinate);
            e.Handled = true;
            return true;
        }

        return false;
    }

    public override bool OnMouseMove(MouseEventArgs e)
    {
        if (_isDragging && _selectedVertex != null && e.WorldCoordinate != null)
        {
            MoveVertex(_selectedVertex, e.WorldCoordinate);
            e.Handled = true;
            return true;
        }

        return false;
    }

    public override bool OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButton.Left && _isDragging)
        {
            CompleteVertexMove();
            _isDragging = false;
            _selectedVertex = null;
            e.Handled = true;
            return true;
        }

        return false;
    }

    public override bool OnKeyDown(KeyEventArgs e)
    {
        if (base.OnKeyDown(e))
            return true;

        if (e.Key == Key.Delete && _selectedVertex != null)
        {
            DeleteVertex(_selectedVertex);
            e.Handled = true;
            return true;
        }

        return false;
    }

    private void UpdateVertexHandles()
    {
        ClearVertexHandles();

        if (_selectedFeature?.Geometry == null)
            return;

        // 도형의 모든 정점에 대해 핸들 생성
        var vertices = ExtractVertices(_selectedFeature.Geometry);
        foreach (var vertex in vertices)
        {
            _vertexHandles.Add(new VertexHandle(vertex));
        }
    }

    private void ClearVertexHandles()
    {
        _vertexHandles.Clear();
        _selectedVertex = null;
    }

    private VertexHandle? GetVertexHandleAt(ICoordinate position)
    {
        const double tolerance = 0.0001; // 선택 허용 오차
        
        return _vertexHandles.FirstOrDefault(h => 
            h.Position.Distance(position) <= tolerance);
    }

    private void MoveVertex(VertexHandle handle, ICoordinate newPosition)
    {
        handle.TemporaryPosition = newPosition;
        // TODO: 미리보기 업데이트
    }

    private void CompleteVertexMove()
    {
        if (_selectedVertex?.TemporaryPosition == null || _selectedFeature == null || _editLayer == null)
            return;

        var originalGeometry = _selectedFeature.Geometry.Clone();
        
        // 정점 위치 업데이트
        UpdateVertexInGeometry(_selectedFeature.Geometry, _selectedVertex.Index, _selectedVertex.TemporaryPosition);
        
        var command = new VertexMoveCommand(_selectedFeature, _editLayer, originalGeometry, _selectedFeature.Geometry.Clone());
        ExecuteCommand(command);
        
        UpdateVertexHandles();
    }

    private void AddVertex(ICoordinate position)
    {
        if (_selectedFeature?.Geometry == null || _editLayer == null)
            return;

        var originalGeometry = _selectedFeature.Geometry.Clone();
        
        // 가장 가까운 세그먼트 찾기
        var segment = FindNearestSegment(_selectedFeature.Geometry, position);
        if (segment != null)
        {
            InsertVertex(_selectedFeature.Geometry, segment.Value.Item1, segment.Value.Item2, position);
            
            var command = new VertexAddCommand(_selectedFeature, _editLayer, originalGeometry, _selectedFeature.Geometry.Clone());
            ExecuteCommand(command);
            
            UpdateVertexHandles();
        }
    }

    private void DeleteVertex(VertexHandle handle)
    {
        if (_selectedFeature?.Geometry == null || _editLayer == null)
            return;

        var originalGeometry = _selectedFeature.Geometry.Clone();
        
        // 정점 삭제
        RemoveVertex(_selectedFeature.Geometry, handle.Index);
        
        var command = new VertexDeleteCommand(_selectedFeature, _editLayer, originalGeometry, _selectedFeature.Geometry.Clone());
        ExecuteCommand(command);
        
        UpdateVertexHandles();
    }

    // 헬퍼 메서드들 (실제 구현은 도형 타입에 따라 다름)
    private List<VertexInfo> ExtractVertices(IGeometry geometry)
    {
        var vertices = new List<VertexInfo>();
        // TODO: 도형 타입별 정점 추출
        return vertices;
    }

    private void UpdateVertexInGeometry(IGeometry geometry, int index, ICoordinate newPosition)
    {
        // TODO: 도형 타입별 정점 업데이트
    }

    private (int, int)? FindNearestSegment(IGeometry geometry, ICoordinate position)
    {
        // TODO: 가장 가까운 세그먼트 찾기
        return null;
    }

    private void InsertVertex(IGeometry geometry, int segmentStart, int segmentEnd, ICoordinate position)
    {
        // TODO: 정점 삽입
    }

    private void RemoveVertex(IGeometry geometry, int index)
    {
        // TODO: 정점 제거
    }
}

/// <summary>
/// 정점 핸들
/// </summary>
public class VertexHandle
{
    public ICoordinate Position { get; set; }
    public ICoordinate? TemporaryPosition { get; set; }
    public int Index { get; set; }
    public bool IsSelected { get; set; }

    public VertexHandle(VertexInfo vertex)
    {
        Position = vertex.Position;
        Index = vertex.Index;
    }
}

/// <summary>
/// 정점 정보
/// </summary>
public class VertexInfo
{
    public ICoordinate Position { get; set; }
    public int Index { get; set; }
    public int RingIndex { get; set; } // 폴리곤의 경우
    public int GeometryIndex { get; set; } // 멀티 지오메트리의 경우

    public VertexInfo(ICoordinate position, int index)
    {
        Position = position;
        Index = index;
    }
}

/// <summary>
/// 정점 이동 명령
/// </summary>
public class VertexMoveCommand : IEditCommand
{
    private readonly IFeature _feature;
    private readonly ILayer _layer;
    private readonly IGeometry _oldGeometry;
    private readonly IGeometry _newGeometry;

    public string Description => "정점 이동";

    public VertexMoveCommand(IFeature feature, ILayer layer, IGeometry oldGeometry, IGeometry newGeometry)
    {
        _feature = feature;
        _layer = layer;
        _oldGeometry = oldGeometry;
        _newGeometry = newGeometry;
    }

    public bool Execute()
    {
        _feature.Geometry = _newGeometry;
        _layer.UpdateFeature(_feature);
        return true;
    }

    public bool Undo()
    {
        _feature.Geometry = _oldGeometry;
        _layer.UpdateFeature(_feature);
        return true;
    }
}

/// <summary>
/// 정점 추가 명령
/// </summary>
public class VertexAddCommand : VertexMoveCommand
{
    public new string Description => "정점 추가";

    public VertexAddCommand(IFeature feature, ILayer layer, IGeometry oldGeometry, IGeometry newGeometry)
        : base(feature, layer, oldGeometry, newGeometry)
    {
    }
}

/// <summary>
/// 정점 삭제 명령
/// </summary>
public class VertexDeleteCommand : VertexMoveCommand
{
    public new string Description => "정점 삭제";

    public VertexDeleteCommand(IFeature feature, ILayer layer, IGeometry oldGeometry, IGeometry newGeometry)
        : base(feature, layer, oldGeometry, newGeometry)
    {
    }
}