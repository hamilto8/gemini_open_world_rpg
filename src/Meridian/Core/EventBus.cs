using System;
using System.Collections.Generic;

namespace Meridian.Core;

/// <summary>
/// Pure C# typed publish/subscribe bus (Section 3.3).
/// <para>
/// <b>Threading:</b> main-thread only. Gameplay events are published from Godot's
/// <c>_Process</c>/<c>_PhysicsProcess</c>, so the bus deliberately carries no locks. Do not publish
/// or subscribe from background threads.
/// </para>
/// <para>
/// Handlers for each event type are stored as an immutable array. <see cref="Publish"/> therefore
/// allocates nothing and is reentrancy-safe: a handler that subscribes or unsubscribes during
/// dispatch affects the <i>next</i> publish, never the in-flight iteration. Subscribe/unsubscribe
/// allocate a new array (rare relative to publish).
/// </para>
/// </summary>
public class EventBus : IEventBus
{
    private readonly Dictionary<Type, object> _handlers = new();

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);
        var existing = _handlers.TryGetValue(eventType, out var listObj)
            ? (Action<TEvent>[])listObj
            : Array.Empty<Action<TEvent>>();

        var updated = new Action<TEvent>[existing.Length + 1];
        Array.Copy(existing, updated, existing.Length);
        updated[existing.Length] = handler;
        _handlers[eventType] = updated;

        return new SubscriptionToken<TEvent>(this, handler);
    }

    public void Publish<TEvent>(TEvent ev)
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var listObj))
        {
            return;
        }

        // Immutable snapshot: safe to iterate even if a handler unsubscribes mid-dispatch.
        var handlers = (Action<TEvent>[])listObj;
        for (int i = 0; i < handlers.Length; i++)
        {
            handlers[i](ev);
        }
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler)
    {
        var eventType = typeof(TEvent);
        if (!_handlers.TryGetValue(eventType, out var listObj))
        {
            return;
        }

        var existing = (Action<TEvent>[])listObj;
        int index = Array.IndexOf(existing, handler);
        if (index < 0)
        {
            return;
        }

        if (existing.Length == 1)
        {
            _handlers.Remove(eventType);
            return;
        }

        var updated = new Action<TEvent>[existing.Length - 1];
        Array.Copy(existing, 0, updated, 0, index);
        Array.Copy(existing, index + 1, updated, index, existing.Length - index - 1);
        _handlers[eventType] = updated;
    }

    public void Clear()
    {
        _handlers.Clear();
    }

    private sealed class SubscriptionToken<TEvent>(EventBus bus, Action<TEvent> handler) : IDisposable
    {
        private EventBus? _bus = bus;
        private Action<TEvent>? _handler = handler;

        public void Dispose()
        {
            var b = _bus;
            var h = _handler;
            if (b != null && h != null)
            {
                // Idempotent: clear our refs first so a double Dispose is a no-op.
                _bus = null;
                _handler = null;
                b.Unsubscribe(h);
            }
        }
    }
}
