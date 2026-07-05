using System;

namespace Meridian.Core;

/// <summary>
/// Typed pub/sub event bus interface.
/// Unrelated systems communicate only through this bus using readonly structs or records.
/// Subscriptions return an IDisposable token which MUST be stored and disposed when the subscriber is destroyed.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Subscribes a handler to events of type TEvent.
    /// Returns an IDisposable token that unsubscribes the handler when disposed.
    /// </summary>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler);

    /// <summary>
    /// Publishes an event of type TEvent to all registered subscribers.
    /// </summary>
    void Publish<TEvent>(TEvent ev);

    /// <summary>
    /// Clears all event subscriptions across all event types.
    /// </summary>
    void Clear();
}
