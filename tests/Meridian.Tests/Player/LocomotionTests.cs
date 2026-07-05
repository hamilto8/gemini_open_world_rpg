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
}
