using Godot;

namespace Meridian.Input;

/// <summary>
/// Registers the default gameplay input actions into Godot's InputMap at boot if they are not already
/// present (e.g. authored in project.godot). Without these, every <c>Input.IsActionPressed(...)</c> call
/// logs an "action does not exist" error each frame and returns false, so the player can't move and the
/// vehicle can't be driven regardless of the input-context fixes (V1).
/// <para>
/// Each action is bound for <b>both</b> keyboard/mouse and an Xbox-style gamepad, so the game responds
/// to whichever device the player uses (the active device for UI glyphs is tracked separately by
/// <see cref="Meridian.Core.IInputDeviceTracker"/>). Keys use physical positions so they are
/// keyboard-layout independent; a future settings/rebinding layer (doc §17) can override these.
/// </para>
/// </summary>
public static class InputMapBootstrap
{
    private const float StickDeadzone = 0.2f;

    public static void EnsureDefaultBindings()
    {
        // Movement: WASD + left stick (analog). Deadzone keeps the stick from drifting at rest.
        AddKey("move_forward", Key.W);
        AddJoyAxis("move_forward", JoyAxis.LeftY, -1.0f);
        AddKey("move_backward", Key.S);
        AddJoyAxis("move_backward", JoyAxis.LeftY, 1.0f);
        AddKey("move_left", Key.A);
        AddJoyAxis("move_left", JoyAxis.LeftX, -1.0f);
        AddKey("move_right", Key.D);
        AddJoyAxis("move_right", JoyAxis.LeftX, 1.0f);
        SetDeadzone("move_forward", StickDeadzone);
        SetDeadzone("move_backward", StickDeadzone);
        SetDeadzone("move_left", StickDeadzone);
        SetDeadzone("move_right", StickDeadzone);

        // Camera look via right stick (analog, read per-frame). Mouse-look is handled separately from
        // InputEventMouseMotion, so these actions are gamepad-only.
        AddJoyAxis("look_left", JoyAxis.RightX, -1.0f);
        AddJoyAxis("look_right", JoyAxis.RightX, 1.0f);
        AddJoyAxis("look_up", JoyAxis.RightY, -1.0f);
        AddJoyAxis("look_down", JoyAxis.RightY, 1.0f);
        SetDeadzone("look_left", StickDeadzone);
        SetDeadzone("look_right", StickDeadzone);
        SetDeadzone("look_up", StickDeadzone);
        SetDeadzone("look_down", StickDeadzone);

        // Buttons — Xbox layout.
        AddKey("jump", Key.Space);
        AddJoyButton("jump", JoyButton.A);
        AddKey("sprint", Key.Shift);
        AddJoyButton("sprint", JoyButton.LeftStick);   // click left stick (L3)
        AddKey("crouch", Key.Ctrl);
        AddJoyButton("crouch", JoyButton.B);
        AddKey("interact", Key.E);
        AddJoyButton("interact", JoyButton.X);
        AddKey("reload", Key.R);
        AddJoyButton("reload", JoyButton.Y);
        AddKey("brake", Key.Space);                     // vehicle handbrake; jump is blocked in the Vehicle context
        AddJoyButton("brake", JoyButton.A);
        AddKey("menu_open", Key.Escape);
        AddJoyButton("menu_open", JoyButton.Start);

        // Fire / aim: mouse buttons + gamepad triggers.
        AddMouseButton("fire", MouseButton.Left);
        AddJoyAxis("fire", JoyAxis.TriggerRight, 1.0f);
        AddMouseButton("aim", MouseButton.Right);
        AddJoyAxis("aim", JoyAxis.TriggerLeft, 1.0f);
        // Note: "look" is an InputContextService permission (gated via IsActionAllowed) and needs no
        // InputMap entry — mouse-look reads InputEventMouseMotion; stick-look reads look_* above.
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

    private static void AddJoyButton(string action, JoyButton button)
    {
        EnsureAction(action);
        foreach (var existing in InputMap.ActionGetEvents(action))
        {
            if (existing is InputEventJoypadButton joy && joy.ButtonIndex == button)
            {
                return;
            }
        }
        InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = button });
    }

    private static void AddJoyAxis(string action, JoyAxis axis, float value)
    {
        EnsureAction(action);
        foreach (var existing in InputMap.ActionGetEvents(action))
        {
            if (existing is InputEventJoypadMotion motion && motion.Axis == axis && Mathf.Sign(motion.AxisValue) == Mathf.Sign(value))
            {
                return;
            }
        }
        InputMap.ActionAddEvent(action, new InputEventJoypadMotion { Axis = axis, AxisValue = value });
    }

    private static void SetDeadzone(string action, float deadzone)
    {
        if (InputMap.HasAction(action))
        {
            InputMap.ActionSetDeadzone(action, deadzone);
        }
    }

    private static void EnsureAction(string action)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }
    }
}
