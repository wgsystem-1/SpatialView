namespace SpatialView.Engine.Events;

/// <summary>
/// 이벤트 버스 인터페이스
/// </summary>
public interface IEventBus : IDisposable
{
    /// <summary>
    /// 이벤트 구독
    /// </summary>
    /// <typeparam name="TEvent">이벤트 타입</typeparam>
    /// <param name="handler">이벤트 핸들러</param>
    /// <returns>구독 토큰</returns>
    IEventSubscription Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent;

    /// <summary>
    /// 비동기 이벤트 구독
    /// </summary>
    /// <typeparam name="TEvent">이벤트 타입</typeparam>
    /// <param name="handler">비동기 이벤트 핸들러</param>
    /// <returns>구독 토큰</returns>
    IEventSubscription Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IEvent;

    /// <summary>
    /// 이벤트 구독 (필터 포함)
    /// </summary>
    /// <typeparam name="TEvent">이벤트 타입</typeparam>
    /// <param name="handler">이벤트 핸들러</param>
    /// <param name="filter">이벤트 필터</param>
    /// <returns>구독 토큰</returns>
    IEventSubscription Subscribe<TEvent>(Action<TEvent> handler, Predicate<TEvent> filter) where TEvent : IEvent;

    /// <summary>
    /// 이벤트 발행
    /// </summary>
    /// <typeparam name="TEvent">이벤트 타입</typeparam>
    /// <param name="eventData">이벤트 데이터</param>
    void Publish<TEvent>(TEvent eventData) where TEvent : IEvent;

    /// <summary>
    /// 비동기 이벤트 발행
    /// </summary>
    /// <typeparam name="TEvent">이벤트 타입</typeparam>
    /// <param name="eventData">이벤트 데이터</param>
    Task PublishAsync<TEvent>(TEvent eventData) where TEvent : IEvent;

    /// <summary>
    /// 구독 해제
    /// </summary>
    /// <param name="subscription">구독 토큰</param>
    void Unsubscribe(IEventSubscription subscription);

    /// <summary>
    /// 특정 타입의 모든 구독 해제
    /// </summary>
    /// <typeparam name="TEvent">이벤트 타입</typeparam>
    void UnsubscribeAll<TEvent>() where TEvent : IEvent;

    /// <summary>
    /// 모든 구독 해제
    /// </summary>
    void UnsubscribeAll();
}


/// <summary>
/// 이벤트 구독 토큰
/// </summary>
public interface IEventSubscription : IDisposable
{
    /// <summary>
    /// 구독 ID
    /// </summary>
    Guid SubscriptionId { get; }

    /// <summary>
    /// 이벤트 타입
    /// </summary>
    Type EventType { get; }

    /// <summary>
    /// 활성 상태
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// 구독 일시 중지
    /// </summary>
    void Pause();

    /// <summary>
    /// 구독 재개
    /// </summary>
    void Resume();
}

/// <summary>
/// 기본 이벤트 클래스
/// </summary>
public abstract class EventBase : IEvent
{
    public Guid EventId { get; }
    public DateTime Timestamp { get; }
    public object? Source { get; }

    protected EventBase(object? source = null)
    {
        EventId = Guid.NewGuid();
        Timestamp = DateTime.Now;
        Source = source;
    }
}

/// <summary>
/// 취소 가능한 이벤트
/// </summary>
public interface ICancelableEvent : IEvent
{
    /// <summary>
    /// 취소 여부
    /// </summary>
    bool IsCancelled { get; set; }

    /// <summary>
    /// 취소 이유
    /// </summary>
    string? CancelReason { get; set; }
}

/// <summary>
/// 취소 가능한 이벤트 기본 클래스
/// </summary>
public abstract class CancelableEventBase : EventBase, ICancelableEvent
{
    public bool IsCancelled { get; set; }
    public string? CancelReason { get; set; }

    protected CancelableEventBase(object? source = null) : base(source)
    {
    }

    public void Cancel(string? reason = null)
    {
        IsCancelled = true;
        CancelReason = reason;
    }
}

#region Map Events

/// <summary>
/// 맵 뷰 변경 이벤트
/// </summary>
public class ViewChangedEvent : EventBase
{
    public Geometry.Envelope OldExtent { get; }
    public Geometry.Envelope NewExtent { get; }
    public double OldZoom { get; }
    public double NewZoom { get; }

    public ViewChangedEvent(object source, Geometry.Envelope oldExtent, Geometry.Envelope newExtent, double oldZoom, double newZoom)
        : base(source)
    {
        OldExtent = oldExtent;
        NewExtent = newExtent;
        OldZoom = oldZoom;
        NewZoom = newZoom;
    }
}

/// <summary>
/// 레이어 추가 이벤트
/// </summary>
public class LayerAddedEvent : CancelableEventBase
{
    public Data.Layers.ILayer Layer { get; }
    public int Index { get; }

    public LayerAddedEvent(object source, Data.Layers.ILayer layer, int index)
        : base(source)
    {
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        Index = index;
    }
}

/// <summary>
/// 레이어 제거 이벤트
/// </summary>
public class LayerRemovedEvent : CancelableEventBase
{
    public Data.Layers.ILayer Layer { get; }
    public int Index { get; }

    public LayerRemovedEvent(object source, Data.Layers.ILayer layer, int index)
        : base(source)
    {
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        Index = index;
    }
}

/// <summary>
/// 피처 선택 이벤트
/// </summary>
public class FeatureSelectedEvent : EventBase
{
    public Data.IFeature? Feature { get; }
    public Data.Layers.ILayer Layer { get; }
    public bool IsMultiSelect { get; }

    public FeatureSelectedEvent(object source, Data.IFeature? feature, Data.Layers.ILayer layer, bool isMultiSelect = false)
        : base(source)
    {
        Feature = feature;
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        IsMultiSelect = isMultiSelect;
    }
}

#endregion

#region Edit Events

/// <summary>
/// 편집 시작 이벤트
/// </summary>
public class BeforeEditEvent : CancelableEventBase
{
    public Data.IFeature Feature { get; }
    public Data.Layers.ILayer Layer { get; }
    public EditOperation Operation { get; }

    public BeforeEditEvent(object source, Data.IFeature feature, Data.Layers.ILayer layer, EditOperation operation)
        : base(source)
    {
        Feature = feature ?? throw new ArgumentNullException(nameof(feature));
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        Operation = operation;
    }
}

/// <summary>
/// 편집 완료 이벤트
/// </summary>
public class AfterEditEvent : EventBase
{
    public Data.IFeature Feature { get; }
    public Data.Layers.ILayer Layer { get; }
    public EditOperation Operation { get; }
    public bool Success { get; }
    public string? ErrorMessage { get; }

    public AfterEditEvent(object source, Data.IFeature feature, Data.Layers.ILayer layer, EditOperation operation, bool success, string? errorMessage = null)
        : base(source)
    {
        Feature = feature ?? throw new ArgumentNullException(nameof(feature));
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        Operation = operation;
        Success = success;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// 편집 작업 종류
/// </summary>
public enum EditOperation
{
    /// <summary>생성</summary>
    Create,
    /// <summary>수정</summary>
    Modify,
    /// <summary>삭제</summary>
    Delete,
    /// <summary>이동</summary>
    Move,
    /// <summary>형상 변경</summary>
    Reshape,
    /// <summary>속성 변경</summary>
    AttributeChange
}

#endregion

#region Plugin Events

/// <summary>
/// 플러그인 메시지 이벤트
/// </summary>
public class PluginMessageEvent : EventBase
{
    public string PluginId { get; }
    public string Message { get; }
    public MessageLevel Level { get; }

    public PluginMessageEvent(object source, string pluginId, string message, MessageLevel level = MessageLevel.Info)
        : base(source)
    {
        PluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Level = level;
    }
}

/// <summary>
/// 메시지 레벨
/// </summary>
public enum MessageLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}

#endregion