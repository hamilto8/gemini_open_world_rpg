using System;
using Godot;

namespace Meridian.Core;

/// <summary>
/// Autoload wrapper for the EventBus. Registers IEventBus with the Services locator at boot.
/// </summary>
public partial class EventBusNode : Node, IEventBus
{
    private readonly EventBus _bus = new();

    public override void _EnterTree()
    {
        Services.Register<IEventBus>(this);
    }

    public override void _ExitTree()
    {
        _bus.Clear();
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) => _bus.Subscribe(handler);

    public void Publish<TEvent>(TEvent ev) => _bus.Publish(ev);

    public void Clear() => _bus.Clear();
}
