using System;
using Xunit;
using Meridian.Core;
using Meridian.Player;

namespace Meridian.Tests.Player;

public class LocomotionTests
{
    [Fact]
    public void Update_ShouldTransitionToSprint_WhenSprintHeldAndMoving()
    {
        var hsm = new LocomotionStateMachine();
        var input = new InputFrame
        {
            MoveX = 1.0f,
            MoveY = 0f,
            SprintHeld = true
        };

        // Inputs, IsOnFloor, Vertical velocity, Horizontal speed length, Current stamina
        hsm.Update(input, true, 0f, 5.0f, 100f);

        Assert.Equal(LocomotionState.Sprint, hsm.CurrentState);
    }

    [Fact]
    public void Update_ShouldTransitionToJump_WhenJumpPressed()
    {
        var hsm = new LocomotionStateMachine();
        var input = new InputFrame { JumpPressed = true };

        hsm.Update(input, true, 0f, 0f, 100f);

        Assert.Equal(LocomotionState.Jump, hsm.CurrentState);
    }

    [Fact]
    public void Update_ShouldTransitionToFall_WhenOffFloorAndVerticalVelocityIsNegative()
    {
        var hsm = new LocomotionStateMachine();
        var input = new InputFrame();

        hsm.Update(input, false, -5.0f, 0f, 100f);

        Assert.Equal(LocomotionState.Fall, hsm.CurrentState);
    }

    [Fact]
    public void Update_ShouldTransitionToCrouch_WhenCrouchHeld()
    {
        var hsm = new LocomotionStateMachine();
        var input = new InputFrame { CrouchHeld = true };

        hsm.Update(input, true, 0f, 0f, 100f);

        Assert.Equal(LocomotionState.Crouch, hsm.CurrentState);
    }

    [Fact]
    public void Update_ShouldDenySprint_WhenStaminaIsZero()
    {
        var hsm = new LocomotionStateMachine();
        var input = new InputFrame { MoveX = 1.0f, SprintHeld = true };

        // Moving and holding sprint but out of stamina -> falls through to Run, not Sprint.
        hsm.Update(input, true, 0f, 5.0f, 0f);

        Assert.Equal(LocomotionState.Run, hsm.CurrentState);
    }

    [Theory]
    [InlineData(2.51f, LocomotionState.Walk)] // exactly the threshold is not yet Run
    [InlineData(2.60f, LocomotionState.Run)]
    [InlineData(0.10f, LocomotionState.Idle)] // exactly the idle threshold is still Idle
    public void Update_WalkRunIdle_ThresholdBoundaries(float horizontalSpeed, LocomotionState expected)
    {
        var hsm = new LocomotionStateMachine();
        hsm.Update(new InputFrame(), true, 0f, horizontalSpeed, 100f);
        Assert.Equal(expected, hsm.CurrentState);
    }

    [Fact]
    public void Update_CrouchTakesPrecedenceOverJump()
    {
        var hsm = new LocomotionStateMachine();
        var input = new InputFrame { CrouchHeld = true, JumpPressed = true };

        hsm.Update(input, true, 0f, 0f, 100f);

        Assert.Equal(LocomotionState.Crouch, hsm.CurrentState);
    }

    [Fact]
    public void Update_ShouldRaiseStateChanged_OnTransition()
    {
        var hsm = new LocomotionStateMachine();
        LocomotionState? from = null, to = null;
        hsm.StateChanged += (o, n) => { from = o; to = n; };

        hsm.Update(new InputFrame { CrouchHeld = true }, true, 0f, 0f, 100f);

        Assert.Equal(LocomotionState.Idle, from);
        Assert.Equal(LocomotionState.Crouch, to);
    }

    [Fact]
    public void Update_AimingOverlay_TracksAimHeld()
    {
        var hsm = new LocomotionStateMachine();
        hsm.Update(new InputFrame { AimHeld = true }, true, 0f, 0f, 100f);
        Assert.True(hsm.Aiming);

        hsm.Update(new InputFrame { AimHeld = false }, true, 0f, 0f, 100f);
        Assert.False(hsm.Aiming);
    }
}
