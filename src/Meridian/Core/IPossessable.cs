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
    public float LookX;
    public float LookY;
    public bool JumpPressed;
    public bool SprintHeld;
    public bool CrouchHeld;
    public bool AimHeld;
    public bool FirePressed;
    public bool InteractPressed;

    /// <summary>Held brake input (vehicles). Distinct from <c>JumpPressed</c>, which is edge-triggered.</summary>
    public bool BrakeHeld;
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
