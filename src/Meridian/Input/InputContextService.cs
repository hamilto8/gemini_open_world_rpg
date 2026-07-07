using System;
using System.Collections.Generic;

namespace Meridian.Input;

/// <summary>
/// Pure C# implementation of IInputContextService.
/// Manages input context stack and routing rules.
/// </summary>
public class InputContextService : IInputContextService
{
    private readonly Stack<InputContextType> _stack = new();
    private readonly Dictionary<InputContextType, HashSet<string>> _contextActions = new();

    public InputContextService()
    {
        InitializeDefaultActions();
        Reset();
    }

    public InputContextType CurrentContext => _stack.Count > 0 ? _stack.Peek() : InputContextType.OnFoot;

    public void PushContext(InputContextType context)
    {
        _stack.Push(context);
    }

    public void PopContext()
    {
        if (_stack.Count > 1)
        {
            _stack.Pop();
        }
    }

    public bool IsActionAllowed(string action)
    {
        if (string.IsNullOrEmpty(action)) return false;

        // Global actions always allowed
        if (action.Equals("console_toggle", StringComparison.OrdinalIgnoreCase) ||
            action.Equals("pause", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var current = CurrentContext;
        if (_contextActions.TryGetValue(current, out var allowed))
        {
            if (allowed.Contains(action)) return true;
        }

        // Prefix-based conventions for UI and debug
        if (current == InputContextType.UI && action.StartsWith("ui_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (current == InputContextType.Console && action.StartsWith("console_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public void RegisterActionForContext(InputContextType context, string action)
    {
        if (string.IsNullOrEmpty(action)) return;

        if (!_contextActions.TryGetValue(context, out var set))
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _contextActions[context] = set;
        }
        set.Add(action);
    }

    public void Reset()
    {
        _stack.Clear();
        _stack.Push(InputContextType.OnFoot);
    }

    private void InitializeDefaultActions()
    {
        // OnFoot defaults
        RegisterActionForContext(InputContextType.OnFoot, "move_forward");
        RegisterActionForContext(InputContextType.OnFoot, "move_backward");
        RegisterActionForContext(InputContextType.OnFoot, "move_left");
        RegisterActionForContext(InputContextType.OnFoot, "move_right");
        RegisterActionForContext(InputContextType.OnFoot, "jump");
        RegisterActionForContext(InputContextType.OnFoot, "sprint");
        RegisterActionForContext(InputContextType.OnFoot, "crouch");
        RegisterActionForContext(InputContextType.OnFoot, "interact");
        RegisterActionForContext(InputContextType.OnFoot, "fire");
        RegisterActionForContext(InputContextType.OnFoot, "aim");
        RegisterActionForContext(InputContextType.OnFoot, "reload");
        RegisterActionForContext(InputContextType.OnFoot, "look");
        RegisterActionForContext(InputContextType.OnFoot, "menu_open");

        // Vehicle defaults: the shared movement actions drive throttle (forward/back) and steering
        // (left/right) via InputFrame.MoveY/MoveX, plus a held brake and the exit interaction.
        // (Section 11.5 — vehicles reuse the possession/input pipeline, not a separate action set.)
        RegisterActionForContext(InputContextType.Vehicle, "move_forward");
        RegisterActionForContext(InputContextType.Vehicle, "move_backward");
        RegisterActionForContext(InputContextType.Vehicle, "move_left");
        RegisterActionForContext(InputContextType.Vehicle, "move_right");
        RegisterActionForContext(InputContextType.Vehicle, "brake");
        RegisterActionForContext(InputContextType.Vehicle, "look");
        RegisterActionForContext(InputContextType.Vehicle, "interact");
        RegisterActionForContext(InputContextType.Vehicle, "menu_open");

        // UI defaults
        RegisterActionForContext(InputContextType.UI, "ui_accept");
        RegisterActionForContext(InputContextType.UI, "ui_cancel");
        RegisterActionForContext(InputContextType.UI, "ui_up");
        RegisterActionForContext(InputContextType.UI, "ui_down");
        RegisterActionForContext(InputContextType.UI, "ui_left");
        RegisterActionForContext(InputContextType.UI, "ui_right");
        RegisterActionForContext(InputContextType.UI, "menu_close");

        // Dialogue defaults
        RegisterActionForContext(InputContextType.Dialogue, "dialogue_advance");
        RegisterActionForContext(InputContextType.Dialogue, "dialogue_choice_1");
        RegisterActionForContext(InputContextType.Dialogue, "dialogue_choice_2");
        RegisterActionForContext(InputContextType.Dialogue, "dialogue_choice_3");
        RegisterActionForContext(InputContextType.Dialogue, "dialogue_choice_4");

        // MapView defaults
        RegisterActionForContext(InputContextType.MapView, "map_pan");
        RegisterActionForContext(InputContextType.MapView, "map_zoom_in");
        RegisterActionForContext(InputContextType.MapView, "map_zoom_out");
        RegisterActionForContext(InputContextType.MapView, "map_fast_travel");
        RegisterActionForContext(InputContextType.MapView, "map_close");
    }
}
