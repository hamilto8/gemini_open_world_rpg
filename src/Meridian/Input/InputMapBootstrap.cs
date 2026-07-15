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
        AddKey("move_forward", InputActions.DefaultKeyboard["move_forward"]);
        AddJoyAxis("move_forward", JoyAxis.LeftY, -1.0f);
        AddKey("move_backward", InputActions.DefaultKeyboard["move_backward"]);
        AddJoyAxis("move_backward", JoyAxis.LeftY, 1.0f);
        AddKey("move_left", InputActions.DefaultKeyboard["move_left"]);
        AddJoyAxis("move_left", JoyAxis.LeftX, -1.0f);
        AddKey("move_right", InputActions.DefaultKeyboard["move_right"]);
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
        AddKey("jump", InputActions.DefaultKeyboard["jump"]);
        AddJoyButton("jump", JoyButton.A);
        AddKey("sprint", InputActions.DefaultKeyboard["sprint"]); // run modifier on keyboard
        AddJoyButton("sprint", JoyButton.RightStick);   // run = click right stick (R3) on the gamepad
        AddKey("crouch", InputActions.DefaultKeyboard["crouch"]);
        AddJoyButton("crouch", JoyButton.B);
        AddKey("interact", InputActions.DefaultKeyboard["interact"]);
        AddJoyButton("interact", JoyButton.X);
        AddKey("reload", InputActions.DefaultKeyboard["reload"]);
        AddJoyButton("reload", JoyButton.Y);
        AddKey("brake", InputActions.DefaultKeyboard["brake"]); // vehicle handbrake; jump is blocked in the Vehicle context
        AddJoyButton("brake", JoyButton.A);
        AddKey("menu_open", Key.Escape);
        AddJoyButton("menu_open", JoyButton.Start);

        // Hold to exit the vehicle: E (keyboard) or B (gamepad). Entry is a normal interact press.
        AddKey("exit_vehicle", Key.E);
        AddJoyButton("exit_vehicle", JoyButton.B);

        // Fire / aim: mouse buttons + gamepad triggers.
        AddMouseButton("fire", MouseButton.Left);
        AddJoyAxis("fire", JoyAxis.TriggerRight, 1.0f);
        AddMouseButton("aim", MouseButton.Right);
        AddJoyAxis("aim", JoyAxis.TriggerLeft, 1.0f);

        // Vehicle throttle on the gamepad: Right Trigger accelerates, Left Trigger reverses.
        // (Keyboard vehicle throttle stays W/S via move_forward/backward.)
        AddJoyAxis("accelerate", JoyAxis.TriggerRight, 1.0f);
        AddJoyAxis("reverse", JoyAxis.TriggerLeft, 1.0f);

        // Note: "look" is an InputContextService permission (gated via IsActionAllowed) and needs no
        // InputMap entry — mouse-look reads InputEventMouseMotion; stick-look reads look_* above.

        // Apply the player's saved keyboard rebindings over the defaults just registered.
        InputRebindStore.Load();
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
