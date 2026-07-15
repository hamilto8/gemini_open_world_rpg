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

    /// <summary>
    /// Replaces a keyboard binding. By default conflicts are rejected; callers can explicitly replace
    /// the other action after presenting that consequence to the player.
    /// </summary>
    public static InputRebindResult Rebind(
        string action,
        Key physicalKey,
        InputConflictResolution resolution = InputConflictResolution.Reject)
    {
        if (!InputMap.HasAction(action) || physicalKey == Key.None)
        {
            return new InputRebindResult(false, null);
        }

        string? conflict = FindConflict(action, physicalKey);
        if (conflict != null && resolution == InputConflictResolution.Reject)
        {
            return new InputRebindResult(false, conflict);
        }

        if (conflict != null)
        {
            RemoveKeyboardBinding(conflict);
        }

        RemoveKeyboardBinding(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = physicalKey });

        Save();
        return new InputRebindResult(true, conflict);
    }

    /// <summary>Returns the colliding action in the same playable input context, if any.</summary>
    public static string? FindConflict(string action, Key physicalKey)
    {
        string scope = InputActions.ConflictScope(action);
        foreach (var (candidate, _) in InputActions.Rebindable)
        {
            if (candidate.Equals(action, System.StringComparison.OrdinalIgnoreCase)
                || InputActions.ConflictScope(candidate) != scope)
            {
                continue;
            }

            if (GetBoundKey(candidate) == physicalKey)
            {
                return candidate;
            }
        }
        return null;
    }

    /// <summary>Restores every keyboard binding from the central defaults and clears the persisted file.</summary>
    public static void RestoreDefaults()
    {
        foreach (var (action, _) in InputActions.Rebindable)
        {
            RemoveKeyboardBinding(action);
            if (InputActions.DefaultKeyboard.TryGetValue(action, out var key))
            {
                InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
            }
        }
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

    private static void RemoveKeyboardBinding(string action)
    {
        if (!InputMap.HasAction(action)) return;
        foreach (var ev in new List<InputEvent>(InputMap.ActionGetEvents(action)))
        {
            if (ev is InputEventKey)
            {
                InputMap.ActionEraseEvent(action, ev);
            }
        }
    }
}

public enum InputConflictResolution
{
    Reject,
    Replace,
}

public readonly record struct InputRebindResult(bool Applied, string? ConflictingAction);
