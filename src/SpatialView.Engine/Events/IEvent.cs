using System;

namespace SpatialView.Engine.Events;

/// <summary>
/// 이벤트 인터페이스
/// </summary>
public interface IEvent
{
    /// <summary>
    /// 이벤트 ID
    /// </summary>
    Guid EventId { get; }
    
    /// <summary>
    /// 이벤트 발생 시간
    /// </summary>
    DateTime Timestamp { get; }
    
    /// <summary>
    /// 이벤트 소스
    /// </summary>
    object? Source { get; }
}