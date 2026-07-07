using Godot;

namespace Meridian.Input;

/// <summary>
/// Registers the default gameplay input actions into Godot's InputMap at boot if they are not already
/// present (e.g. authored in project.godot). Without these, every <c>Input.IsActionPressed(...)</c> call
/// logs an "action does not exist" error each frame and returns false, so the player can't move and the
/// vehicle can't be driven regardless of the input-context fixes (V1).
/// <para>
/// Bindings use <b>physical</b> key positions so they are keyboard-layout independent. A future
/// settings/rebinding layer (doc §17) can override these; existing events are never duplicated.
/// </para>
/// </summary>
public static class InputMapBootstrap
{
    public static void EnsureDefaultBindings()
    {
        AddKey("move_forward", Key.W);
        AddKey("move_backward", Key.S);
        AddKey("move_left", Key.A);
        AddKey("move_right", Key.D);
        AddKey("jump", Key.Space);
        AddKey("sprint", Key.Shift);
        AddKey("crouch", Key.Ctrl);
        AddKey("interact", Key.E);
        AddKey("reload", Key.R);
        AddKey("brake", Key.Space);          // vehicle handbrake — jump is blocked in the Vehicle context
        AddKey("menu_open", Key.Escape);

        AddMouseButton("fire", MouseButton.Left);
        AddMouseButton("aim", MouseButton.Right);
        // Note: "look" is an InputContextService concept only (gated via IsActionAllowed) and needs no
        // InputMap entry — mouse-look is read from InputEventMouseMotion, not a named action.
    }

    private static void AddKey(string action, Key physicalKey)
    {
        EnsureAction(action);
        foreach (var existing in InputMap.ActionGetEvents(action))
        {
            if (existing is InputEventKey key && key.PhysicalKeycode == physicalKey)
            {
                return; // already bound (e.g. authored in project.godot)
            }
        }
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = physicalKey });
    }

    private static void AddMouseButton(string action, MouseButton button)
    {
        EnsureAction(action);
        foreach (var existing in InputMap.ActionGetEvents(action))
        {
            if (existing is InputEventMouseButton mouse && mouse.ButtonIndex == button)
            {
                return;
            }
        }
        InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
    }

    private static void EnsureAction(string action)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }
    }
}
