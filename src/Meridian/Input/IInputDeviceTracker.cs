namespace Meridian.Input;

/// <summary>The physical device the player is currently driving the game with.</summary>
public enum InputDeviceType
{
    KeyboardMouse,
    Gamepad,
}

/// <summary>
/// Tracks the most recently used input device so the UI can swap control schemes / button glyphs on
/// the fly. Both devices are always live (every action is bound for both); this only reflects which
/// one the player last touched.
/// </summary>
public interface IInputDeviceTracker
{
    /// <summary>The device the player most recently produced input on.</summary>
    InputDeviceType ActiveDevice { get; }
}

/// <summary>
/// Published on the EventBus when the active input device flips (keyboard/mouse ↔ gamepad), so UI can
/// re-render prompts. Fires only on a change, never per event.
/// </summary>
public readonly record struct InputDeviceChangedEvent(InputDeviceType Device);
