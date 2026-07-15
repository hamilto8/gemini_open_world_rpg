using System;
using System.Collections.Generic;
using Godot;

namespace Meridian.UI;

/// <summary>Owns instantiated screens, back-stack order, focus restoration, and screen lifecycle.</summary>
public sealed class UIScreenStack
{
    private readonly Control _host;
    private readonly UIScreenRegistry _registry;
    private readonly Stack<(UIScreenId Id, UIScreen Screen)> _stack = new();

    public UIScreenStack(Control host, UIScreenRegistry registry)
    {
        _host = host;
        _registry = registry;
    }

    public int Count => _stack.Count;
    public UIScreenId? Current => _stack.TryPeek(out var top) ? top.Id : null;
    public event Action? Changed;

    public bool Push(UIScreenId id)
    {
        if (!_registry.TryGet(id, out var definition) || definition?.Scene == null) return false;
        if (_stack.TryPeek(out var previous)) previous.Screen.Visible = false;

        if (definition.Scene.Instantiate() is not UIScreen screen)
        {
            if (_stack.TryPeek(out previous)) previous.Screen.Visible = true;
            return false;
        }

        _host.AddChild(screen);
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.BackRequested += Pop;
        screen.ScreenRequested += rawId => Push((UIScreenId)rawId);
        _stack.Push((id, screen));
        screen.CallDeferred(nameof(UIScreen.FocusDefault));
        Changed?.Invoke();
        return true;
    }

    public bool Replace(UIScreenId id)
    {
        if (_stack.Count > 0) RemoveTop();
        return Push(id);
    }

    public void Pop()
    {
        if (_stack.Count == 0) return;
        RemoveTop();
        if (_stack.TryPeek(out var previous))
        {
            previous.Screen.Visible = true;
            previous.Screen.CallDeferred(nameof(UIScreen.FocusDefault));
        }
        Changed?.Invoke();
    }

    public void Clear()
    {
        while (_stack.Count > 0) RemoveTop();
        Changed?.Invoke();
    }

    private void RemoveTop()
    {
        var top = _stack.Pop();
        top.Screen.QueueFree();
    }
}
