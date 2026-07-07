using System.Collections.Generic;
using Godot;

namespace Meridian.Input;

/// <summary>
/// Persists user keyboard rebindings to <c>user://input_bindings.cfg</c> and re-applies them over the
/// default InputMap at boot. Only the keyboard event per action is stored; gamepad bindings are fixed
/// (Section 17 rebinding). Physical keycodes are saved so bindings stay layout-independent.
/// </summary>
public static class InputRebindStore
{
    private const string ConfigPath = "user://input_bindings.cfg";
    private const string Section = "keyboard";

    /// <summary>Replaces the keyboard event bound to <paramref name="action"/> with the given physical key.</summary>
    public static void Rebind(string action, Key physicalKey)
    {
        if (!InputMap.HasAction(action))
        {
            return;
        }

        // Erase existing keyboard events (keep gamepad/mouse), then add the new key.
        foreach (var ev in new List<InputEvent>(InputMap.ActionGetEvents(action)))
        {
            if (ev is InputEventKey)
            {
                InputMap.ActionEraseEvent(action, ev);
            }
        }
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = physicalKey });

        Save();
    }

    /// <summary>Returns the physical key currently bound to the action's keyboard event, or Key.None.</summary>
    public static Key GetBoundKey(string action)
    {
        if (InputMap.HasAction(action))
        {
            foreach (var ev in InputMap.ActionGetEvents(action))
            {
                if (ev is InputEventKey key)
                {
                    return key.PhysicalKeycode;
                }
            }
        }
        return Key.None;
    }

    /// <summary>Writes every rebindable action's current keyboard binding to disk.</summary>
    public static void Save()
    {
        var config = new ConfigFile();
        foreach (var (action, _) in InputActions.Rebindable)
        {
            Key key = GetBoundKey(action);
            if (key != Key.None)
            {
                config.SetValue(Section, action, (int)key);
            }
        }
        config.Save(ConfigPath);
    }

    /// <summary>Applies saved rebindings over the current (default) InputMap. Call after defaults exist.</summary>
    public static void Load()
    {
        var config = new ConfigFile();
        if (config.Load(ConfigPath) != Error.Ok || !config.HasSection(Section))
        {
            return;
        }

        foreach (var action in config.GetSectionKeys(Section))
        {
            if (!InputMap.HasAction(action))
            {
                continue;
            }

            var key = (Key)config.GetValue(Section, action).AsInt32();
            foreach (var ev in new List<InputEvent>(InputMap.ActionGetEvents(action)))
            {
                if (ev is InputEventKey)
                {
                    InputMap.ActionEraseEvent(action, ev);
                }
            }
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
        }
    }
}
