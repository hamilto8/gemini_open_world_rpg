using System;

namespace Meridian.Core;

/// <summary>
/// Immutable input frame containing snapshot of player inputs for a single frame.
/// Decouples physical input devices from possessed entities.
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
}

/// <summary>
/// Interface implemented by any entity that can be possessed and driven by the PlayerController.
/// </summary>
public interface IPossessable
{
    /// <summary>
    /// Called when the PlayerController possesses this entity.
    /// </summary>
    void OnPossessed(PlayerControllerNode controller);

    /// <summary>
    /// Called when the PlayerController releases this entity.
    /// </summary>
    void OnReleased();

    /// <summary>
    /// Receives player input for execution during physics processing.
    /// </summary>
    void ReceiveFrameInput(InputFrame input);
}
