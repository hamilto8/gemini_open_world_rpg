using System;
using Meridian.Core;

namespace Meridian.Player;

/// <summary>
/// Predefined locomotion states.
/// </summary>
public enum LocomotionState
{
    Idle,
    Walk,
    Run,
    Sprint,
    Crouch,
    Jump,
    Fall
}

/// <summary>
/// Pure C# Locomotion Hierarchical State Machine (HSM).
/// Keeps states decoupled from Godot's physics tick for easy testing.
/// Enforces Section 5.3 requirements.
/// </summary>
public class LocomotionStateMachine
{
    public LocomotionState CurrentState { get; private set; } = LocomotionState.Idle;
    public bool Aiming { get; set; } = false;

    public event Action<LocomotionState, LocomotionState>? StateChanged;

    public void Update(InputFrame input, bool isOnFloor, float velocityVertical, float velocityHorizontalLength, float currentStamina)
    {
        LocomotionState oldState = CurrentState;

        // Process aiming overlay flag separately (Section 5.3 Aiming overlay)
        Aiming = input.AimHeld;

        // State Transition Logic
        if (isOnFloor)
        {
            if (input.CrouchHeld)
            {
                CurrentState = LocomotionState.Crouch;
            }
            else if (input.JumpPressed)
            {
                CurrentState = LocomotionState.Jump;
            }
            else if (input.SprintHeld && velocityHorizontalLength > 0.1f && currentStamina > 0.0f)
            {
                CurrentState = LocomotionState.Sprint;
            }
            else if (velocityHorizontalLength > 2.51f)
            {
                CurrentState = LocomotionState.Run;
            }
            else if (velocityHorizontalLength > 0.1f)
            {
                CurrentState = LocomotionState.Walk;
            }
            else
            {
                CurrentState = LocomotionState.Idle;
            }
        }
        else
        {
            // Airborne transitions
            if (velocityVertical > 0.1f)
            {
                CurrentState = LocomotionState.Jump;
            }
            else
            {
                CurrentState = LocomotionState.Fall;
            }
        }

        if (CurrentState != oldState)
        {
            StateChanged?.Invoke(oldState, CurrentState);
        }
    }

    public void ForceState(LocomotionState state)
    {
        LocomotionState old = CurrentState;
        CurrentState = state;
        if (old != state)
        {
            StateChanged?.Invoke(old, state);
        }
    }
}
