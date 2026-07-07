namespace Meridian.Input;

/// <summary>
/// Central metadata for gameplay input actions: the rebindable keyboard set (with display names) and
/// the fixed Xbox controller reference layout. Shared by the InputMap bootstrap, the rebind store, and
/// the controls UI so there is one source of truth.
/// </summary>
public static class InputActions
{
    /// <summary>Rebindable keyboard actions with their human-readable labels, in display order.</summary>
    public static readonly (string Action, string Label)[] Rebindable =
    {
        ("move_forward", "Move Forward"),
        ("move_backward", "Move Backward"),
        ("move_left", "Move Left"),
        ("move_right", "Move Right"),
        ("jump", "Jump"),
        ("sprint", "Run"),
        ("crouch", "Crouch"),
        ("interact", "Interact"),
        ("fire", "Fire"),
        ("aim", "Aim"),
        ("reload", "Reload"),
        ("brake", "Vehicle Brake"),
    };

    /// <summary>Fixed Xbox controller layout (button → what it does), for the reference panel.</summary>
    public static readonly (string Button, string Action)[] ControllerLayout =
    {
        ("Left Stick", "Move"),
        ("Right Stick", "Look / Aim camera"),
        ("R3 (click Right Stick)", "Run"),
        ("A", "Jump"),
        ("B", "Crouch"),
        ("X", "Interact / Board"),
        ("Y", "Reload"),
        ("Right Trigger", "Fire"),
        ("Left Trigger", "Aim"),
        ("Start", "Pause Menu"),
    };
}
