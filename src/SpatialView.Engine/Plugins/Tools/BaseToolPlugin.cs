namespace SpatialView.Engine.Plugins.Tools;

/// <summary>
/// 도구 플러그인 기본 클래스
/// </summary>
public abstract class BaseToolPlugin : IToolPlugin
{
    private bool _disposed;
    private bool _isActive;
    private PluginState _state = PluginState.NotInitialized;
    protected IPluginContext? Context { get; private set; }

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract Version Version { get; }
    public abstract string Author { get; }

    public PluginState State => _state;
    public PluginType Type => PluginType.Tool;
    public virtual Version MinEngineVersion => new Version(1, 0, 0, 0);
    public virtual IReadOnlyList<string> Dependencies => Array.Empty<string>();

    public abstract string ToolName { get; }
    public abstract string? ToolIcon { get; }
    public abstract string ToolCategory { get; }
    public bool IsActive => _isActive;

    public virtual async Task<bool> InitializeAsync(IPluginContext context)
    {
        if (_state != PluginState.NotInitialized)
            return false;

        try
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            _state = PluginState.Initializing;

            // 파생 클래스의 초기화
            var result = await OnInitializeAsync(context);
            
            _state = result ? PluginState.Initialized : PluginState.Error;
            return result;
        }
        catch
        {
            _state = PluginState.Error;
            throw;
        }
    }

    public virtual async Task StartAsync()
    {
        if (_state != PluginState.Initialized && _state != PluginState.Stopped)
            throw new InvalidOperationException($"Cannot start plugin in state {_state}");

        _state = PluginState.Started;
        await OnStartAsync();
    }

    public virtual async Task StopAsync()
    {
        if (_state != PluginState.Started)
            return;

        await OnStopAsync();
        _state = PluginState.Stopped;
    }

    public virtual IPluginSettings? GetSettings()
    {
        return null;
    }

    public virtual void ApplySettings(IPluginSettings settings)
    {
        // 파생 클래스에서 구현
    }

    public virtual void Activate()
    {
        if (_state != PluginState.Started)
            throw new InvalidOperationException("Plugin must be started before activation");

        _isActive = true;
        OnActivate();
    }

    public virtual void Deactivate()
    {
        _isActive = false;
        OnDeactivate();
    }

    #region Abstract Methods

    /// <summary>
    /// 플러그인 초기화
    /// </summary>
    protected abstract Task<bool> OnInitializeAsync(IPluginContext context);

    /// <summary>
    /// 플러그인 시작
    /// </summary>
    protected virtual Task OnStartAsync() => Task.CompletedTask;

    /// <summary>
    /// 플러그인 중지
    /// </summary>
    protected virtual Task OnStopAsync() => Task.CompletedTask;

    /// <summary>
    /// 도구 활성화
    /// </summary>
    protected abstract void OnActivate();

    /// <summary>
    /// 도구 비활성화
    /// </summary>
    protected abstract void OnDeactivate();

    #endregion

    #region Mouse and Keyboard Events

    public virtual bool OnMouseDown(MouseEventArgs e)
    {
        return false;
    }

    public virtual bool OnMouseMove(MouseEventArgs e)
    {
        return false;
    }

    public virtual bool OnMouseUp(MouseEventArgs e)
    {
        return false;
    }

    public virtual bool OnKeyDown(KeyEventArgs e)
    {
        return false;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 로그 기록
    /// </summary>
    protected void Log(string message, LogLevel level = LogLevel.Info)
    {
        switch (level)
        {
            case LogLevel.Debug:
                Context?.Logger.LogDebug(message);
                break;
            case LogLevel.Info:
                Context?.Logger.LogInfo(message);
                break;
            case LogLevel.Warning:
                Context?.Logger.LogWarning(message);
                break;
            case LogLevel.Error:
                Context?.Logger.LogError(message);
                break;
        }
    }

    /// <summary>
    /// 이벤트 발행
    /// </summary>
    protected void PublishEvent<TEvent>(TEvent eventData) where TEvent : Events.IEvent
    {
        Context?.EventBus.Publish(eventData);
    }

    /// <summary>
    /// 이벤트 구독
    /// </summary>
    protected Events.IEventSubscription SubscribeEvent<TEvent>(Action<TEvent> handler) where TEvent : Events.IEvent
    {
        return Context?.EventBus.Subscribe(handler) ?? throw new InvalidOperationException("Context not initialized");
    }

    #endregion

    protected enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            if (_state == PluginState.Started)
            {
                StopAsync().Wait(TimeSpan.FromSeconds(5));
            }

            OnDispose();
        }
        catch
        {
            // 무시
        }

        _disposed = true;
    }

    protected virtual void OnDispose()
    {
        // 파생 클래스에서 리소스 정리
    }
}

/// <summary>
/// 선택 도구 플러그인 샘플
/// </summary>
public class SelectToolPlugin : BaseToolPlugin
{
    private Events.IEventSubscription? _mapClickSubscription;

    public override string Id => "SpatialView.Tools.Select";
    public override string Name => "선택 도구";
    public override string Description => "피처를 선택하는 도구";
    public override Version Version => new Version(1, 0, 0, 0);
    public override string Author => "SpatialView Team";

    public override string ToolName => "선택";
    public override string? ToolIcon => null; // Base64 또는 리소스 경로
    public override string ToolCategory => "편집";

    protected override Task<bool> OnInitializeAsync(IPluginContext context)
    {
        Log("선택 도구 초기화 중...");
        return Task.FromResult(true);
    }

    protected override void OnActivate()
    {
        Log("선택 도구 활성화");
        
        // 맵 캔버스 커서 변경
        if (Context?.MapCanvas != null)
        {
            // Context.MapCanvas.Cursor = Cursors.Arrow;
        }
    }

    protected override void OnDeactivate()
    {
        Log("선택 도구 비활성화");
    }

    public override bool OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButton.Left || e.WorldCoordinate == null)
            return false;

        // 클릭 지점에서 피처 찾기
        var layers = Context?.Layers != null ? Context.Layers.ToArray() : Array.Empty<Data.Layers.ILayer>();
        
        foreach (var layer in layers.Where(l => l.IsVisible && l.IsSelectable))
        {
            var features = layer.GetFeatures(new Geometry.Envelope(
                e.WorldCoordinate.X - 0.001,
                e.WorldCoordinate.Y - 0.001,
                e.WorldCoordinate.X + 0.001,
                e.WorldCoordinate.Y + 0.001));

            var feature = features?.FirstOrDefault();
            if (feature != null)
            {
                // 선택 이벤트 발생
                PublishEvent(new Events.FeatureSelectedEvent(this, feature, layer, 
                    (e.Modifiers & ModifierKeys.Control) == ModifierKeys.Control));
                
                e.Handled = true;
                return true;
            }
        }

        // 빈 곳 클릭 시 선택 해제
        PublishEvent(new Events.FeatureSelectedEvent(this, null, null!));
        e.Handled = true;
        return true;
    }
}

/// <summary>
/// 이동 도구 플러그인 샘플
/// </summary>
public class PanToolPlugin : BaseToolPlugin
{
    private bool _isPanning;
    private Geometry.ICoordinate? _lastPosition;

    public override string Id => "SpatialView.Tools.Pan";
    public override string Name => "이동 도구";
    public override string Description => "맵을 이동하는 도구";
    public override Version Version => new Version(1, 0, 0, 0);
    public override string Author => "SpatialView Team";

    public override string ToolName => "이동";
    public override string? ToolIcon => null;
    public override string ToolCategory => "탐색";

    protected override Task<bool> OnInitializeAsync(IPluginContext context)
    {
        Log("이동 도구 초기화 중...");
        return Task.FromResult(true);
    }

    protected override void OnActivate()
    {
        Log("이동 도구 활성화");
        // 손 모양 커서로 변경
    }

    protected override void OnDeactivate()
    {
        Log("이동 도구 비활성화");
        _isPanning = false;
        _lastPosition = null;
    }

    public override bool OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButton.Left && e.WorldCoordinate != null)
        {
            _isPanning = true;
            _lastPosition = e.WorldCoordinate;
            e.Handled = true;
            return true;
        }
        return false;
    }

    public override bool OnMouseMove(MouseEventArgs e)
    {
        if (_isPanning && e.WorldCoordinate != null && _lastPosition != null)
        {
            // 이동 거리 계산
            var dx = e.WorldCoordinate.X - _lastPosition.X;
            var dy = e.WorldCoordinate.Y - _lastPosition.Y;

            // 맵 이동
            var mapCanvas = Context?.MapCanvas;
            if (mapCanvas != null)
            {
                var currentExtent = mapCanvas.ViewExtent;
                var newExtent = new Geometry.Envelope(
                    currentExtent.MinX - dx,
                    currentExtent.MinY - dy,
                    currentExtent.MaxX - dx,
                    currentExtent.MaxY - dy);

                // 뷰 변경 이벤트 발생
                PublishEvent(new Events.ViewChangedEvent(this, currentExtent, newExtent, 
                    mapCanvas.ZoomLevel, mapCanvas.ZoomLevel));
            }

            _lastPosition = e.WorldCoordinate;
            e.Handled = true;
            return true;
        }
        return false;
    }

    public override bool OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButton.Left && _isPanning)
        {
            _isPanning = false;
            _lastPosition = null;
            e.Handled = true;
            return true;
        }
        return false;
    }
}