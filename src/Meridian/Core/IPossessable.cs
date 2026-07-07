using System;

namespace Meridian.Core;

/// <summary>
/// Per-frame snapshot of player inputs, compiled once per physics tick by the
/// <see cref="PlayerControllerNode"/> and handed to the possessed entity. Decouples physical
/// input devices from possessed entities. Fields are write-once during compilation and read-only
/// thereafter; <c>LookX</c>/<c>LookY</c> carry the camera look delta.
/// </summary>
public struct InputFrame
{
    public float MoveX;
    public float MoveY;

    /// <summary>Mouse-look delta in pixels this frame (device: keyboard/mouse).</summary>
    public float LookX;
    public float LookY;

    /// <summary>Right-stick look, normalized -1..1 (device: gamepad). Applied as a rate, not a delta.</summary>
    public float LookStickX;
    public float LookStickY;

    public bool JumpPressed;
    public bool SprintHeld;
    public bool CrouchHeld;
    public bool AimHeld;
    public bool FirePressed;
    public bool ReloadPressed;
    public bool InteractPressed;

    /// <summary>Held brake input (vehicles). Distinct from <c>JumpPressed</c>, which is edge-triggered.</summary>
    public bool BrakeHeld;

    /// <summary>Vehicle throttle in [-1, 1]: keyboard W/S plus gamepad Right/Left triggers.</summary>
    public float VehicleThrottle;

    /// <summary>Held "exit vehicle" input (E / B); the vehicle exits on a long press.</summary>
    public bool ExitVehicleHeld;
}

/// <summary>
/// Interface implemented by any entity that can be possessed and driven by the PlayerController.
/// </summary>
public interface IPossessable
{
    /// <summary>
    /// Called when the PlayerController possesses this entity. Depends on the
    /// <see cref="IPlayerController"/> abstraction so possessables stay headless-testable (Section 3.5).
    /// </summary>
    void OnPossessed(IPlayerController controller);

    /// <summary>
    /// Called when the PlayerController releases this entity.
    /// </summary>
    void OnReleased();

    /// <summary>
    /// Receives player input for execution during physics processing.
    /// </summary>
    void ReceiveFrameInput(InputFrame input);
}

/// <summary>
/// Published on the EventBus whenever the PlayerController's possessed entity changes (including to
/// null on release). Systems that push stat modifiers onto the possessed body — e.g. weather — subscribe
/// to migrate them across possession swaps (V3).
/// </summary>
public readonly record struct PossessionChangedEvent(IPossessable? NewEntity);
