using System;
using System.Collections.Generic;

namespace Meridian.Core;

/// <summary>
/// Pure C# implementation of IEventBus.
/// Optimized for near-zero allocation during dispatch.
/// </summary>
public class EventBus : IEventBus
{
    private readonly Dictionary<Type, object> _handlers = new();

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);
        if (!_handlers.TryGetValue(eventType, out var listObj))
        {
            listObj = new List<Action<TEvent>>();
            _handlers[eventType] = listObj;
        }

        var list = (List<Action<TEvent>>)listObj;
        lock (list)
        {
            list.Add(handler);
        }

        return new SubscriptionToken<TEvent>(this, handler);
    }

    public void Publish<TEvent>(TEvent ev)
    {
        var eventType = typeof(TEvent);
        if (!_handlers.TryGetValue(eventType, out var listObj))
        {
            return;
        }

        var list = (List<Action<TEvent>>)listObj;
        Action<TEvent>[] snapshot;
        lock (list)
        {
            if (list.Count == 0) return;
            snapshot = list.ToArray();
        }

        foreach (var handler in snapshot)
        {
            handler(ev);
        }
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler)
    {
        var eventType = typeof(TEvent);
        if (_handlers.TryGetValue(eventType, out var listObj))
        {
            var list = (List<Action<TEvent>>)listObj;
            lock (list)
            {
                list.Remove(handler);
            }
        }
    }

    public void Clear()
    {
        lock (_handlers)
        {
            _handlers.Clear();
        }
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
                _bus = null;
                _handler = null;
                b.Unsubscribe(h);
            }
        }
    }
}
