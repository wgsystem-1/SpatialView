using System.Collections.Concurrent;

namespace SpatialView.Engine.Events;

/// <summary>
/// 이벤트 버스 구현
/// </summary>
public class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<IEventHandler>> _handlers = new();
    private readonly object _lockObject = new();
    private bool _disposed;

    public IEventSubscription Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);
        var eventHandler = new EventHandler<TEvent>(handler);

        lock (_lockObject)
        {
            if (!_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<IEventHandler>();
                _handlers[eventType] = handlers;
            }

            handlers.Add(eventHandler);
        }

        var subscription = new EventSubscription(this, eventType, eventHandler);
        return subscription;
    }

    public IEventSubscription Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);
        var eventHandler = new AsyncEventHandler<TEvent>(handler);

        lock (_lockObject)
        {
            if (!_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<IEventHandler>();
                _handlers[eventType] = handlers;
            }

            handlers.Add(eventHandler);
        }

        var subscription = new EventSubscription(this, eventType, eventHandler);
        return subscription;
    }

    public IEventSubscription Subscribe<TEvent>(Action<TEvent> handler, Predicate<TEvent> filter) where TEvent : IEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        if (filter == null)
            throw new ArgumentNullException(nameof(filter));

        var eventType = typeof(TEvent);
        var eventHandler = new FilteredEventHandler<TEvent>(handler, filter);

        lock (_lockObject)
        {
            if (!_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<IEventHandler>();
                _handlers[eventType] = handlers;
            }

            handlers.Add(eventHandler);
        }

        var subscription = new EventSubscription(this, eventType, eventHandler);
        return subscription;
    }

    public void Publish<TEvent>(TEvent eventData) where TEvent : IEvent
    {
        if (eventData == null)
            throw new ArgumentNullException(nameof(eventData));

        var eventType = typeof(TEvent);
        var handlers = GetHandlers(eventType);

        foreach (var handler in handlers)
        {
            try
            {
                handler.Handle(eventData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling event {eventType.Name}: {ex.Message}");
            }
        }
    }

    public async Task PublishAsync<TEvent>(TEvent eventData) where TEvent : IEvent
    {
        if (eventData == null)
            throw new ArgumentNullException(nameof(eventData));

        var eventType = typeof(TEvent);
        var handlers = GetHandlers(eventType);

        var tasks = new List<Task>();

        foreach (var handler in handlers)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await handler.HandleAsync(eventData);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error handling event {eventType.Name}: {ex.Message}");
                }
            }));
        }

        await Task.WhenAll(tasks);
    }

    public void Unsubscribe(IEventSubscription subscription)
    {
        if (subscription == null)
            throw new ArgumentNullException(nameof(subscription));

        var eventSubscription = subscription as EventSubscription;
        if (eventSubscription?.Handler == null)
            return;

        lock (_lockObject)
        {
            if (_handlers.TryGetValue(subscription.EventType, out var handlers))
            {
                handlers.Remove(eventSubscription.Handler);
                
                if (handlers.Count == 0)
                {
                    _handlers.TryRemove(subscription.EventType, out _);
                }
            }
        }
    }

    public void UnsubscribeAll<TEvent>() where TEvent : IEvent
    {
        var eventType = typeof(TEvent);

        lock (_lockObject)
        {
            _handlers.TryRemove(eventType, out _);
        }
    }

    public void UnsubscribeAll()
    {
        lock (_lockObject)
        {
            _handlers.Clear();
        }
    }

    private List<IEventHandler> GetHandlers(Type eventType)
    {
        lock (_lockObject)
        {
            var handlers = new List<IEventHandler>();

            // 직접 핸들러
            if (_handlers.TryGetValue(eventType, out var directHandlers))
            {
                handlers.AddRange(directHandlers.Where(h => h.IsActive));
            }

            // 베이스 타입 핸들러 (상속 지원)
            var baseType = eventType.BaseType;
            while (baseType != null && typeof(IEvent).IsAssignableFrom(baseType))
            {
                if (_handlers.TryGetValue(baseType, out var baseHandlers))
                {
                    handlers.AddRange(baseHandlers.Where(h => h.IsActive));
                }
                baseType = baseType.BaseType;
            }

            // 인터페이스 핸들러
            foreach (var interfaceType in eventType.GetInterfaces())
            {
                if (typeof(IEvent).IsAssignableFrom(interfaceType))
                {
                    if (_handlers.TryGetValue(interfaceType, out var interfaceHandlers))
                    {
                        handlers.AddRange(interfaceHandlers.Where(h => h.IsActive));
                    }
                }
            }

            return handlers;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        UnsubscribeAll();
        _disposed = true;
    }

    #region Inner Classes

    /// <summary>
    /// 이벤트 핸들러 인터페이스
    /// </summary>
    private interface IEventHandler
    {
        bool IsActive { get; set; }
        void Handle(object eventData);
        Task HandleAsync(object eventData);
    }

    /// <summary>
    /// 동기 이벤트 핸들러
    /// </summary>
    private class EventHandler<TEvent> : IEventHandler where TEvent : IEvent
    {
        private readonly Action<TEvent> _handler;
        public bool IsActive { get; set; } = true;

        public EventHandler(Action<TEvent> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void Handle(object eventData)
        {
            if (eventData is TEvent typedEvent)
            {
                _handler(typedEvent);
            }
        }

        public Task HandleAsync(object eventData)
        {
            Handle(eventData);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 비동기 이벤트 핸들러
    /// </summary>
    private class AsyncEventHandler<TEvent> : IEventHandler where TEvent : IEvent
    {
        private readonly Func<TEvent, Task> _handler;
        public bool IsActive { get; set; } = true;

        public AsyncEventHandler(Func<TEvent, Task> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void Handle(object eventData)
        {
            if (eventData is TEvent typedEvent)
            {
                _handler(typedEvent).Wait();
            }
        }

        public async Task HandleAsync(object eventData)
        {
            if (eventData is TEvent typedEvent)
            {
                await _handler(typedEvent);
            }
        }
    }

    /// <summary>
    /// 필터링된 이벤트 핸들러
    /// </summary>
    private class FilteredEventHandler<TEvent> : IEventHandler where TEvent : IEvent
    {
        private readonly Action<TEvent> _handler;
        private readonly Predicate<TEvent> _filter;
        public bool IsActive { get; set; } = true;

        public FilteredEventHandler(Action<TEvent> handler, Predicate<TEvent> filter)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
        }

        public void Handle(object eventData)
        {
            if (eventData is TEvent typedEvent && _filter(typedEvent))
            {
                _handler(typedEvent);
            }
        }

        public Task HandleAsync(object eventData)
        {
            Handle(eventData);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 이벤트 구독 구현
    /// </summary>
    private class EventSubscription : IEventSubscription
    {
        private readonly EventBus _eventBus;
        internal readonly IEventHandler Handler;
        
        public Guid SubscriptionId { get; }
        public Type EventType { get; }
        public bool IsActive => Handler?.IsActive ?? false;

        public EventSubscription(EventBus eventBus, Type eventType, IEventHandler handler)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            SubscriptionId = Guid.NewGuid();
        }

        public void Pause()
        {
            if (Handler != null)
                Handler.IsActive = false;
        }

        public void Resume()
        {
            if (Handler != null)
                Handler.IsActive = true;
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe(this);
        }
    }

    #endregion
}