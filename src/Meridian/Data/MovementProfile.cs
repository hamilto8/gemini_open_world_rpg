using Godot;

namespace Meridian.Data;

/// <summary>
/// Immutable movement profile configuration data contract.
/// Enforces Section 5.2 (MovementMotor) requirements.
/// </summary>
[GlobalClass]
public partial class MovementProfile : Resource
{
    // Two gaits: WalkSpeed is the analog-stick ceiling; RunSpeed is reached only with the run modifier
    // (R3 on gamepad / Shift on keyboard). CrouchSpeed applies while crouched.
    [Export] public float WalkSpeed { get; set; } = 2.5f;
    [Export] public float RunSpeed { get; set; } = 5.0f;
    [Export] public float CrouchSpeed { get; set; } = 1.5f;
    [Export] public float Acceleration { get; set; } = 30.0f;
    [Export] public float Friction { get; set; } = 25.0f;
    [Export] public float JumpVelocity { get; set; } = 5.5f;
    [Export] public float GravityMultiplier { get; set; } = 1.0f;

    // Stamina economy and aim handling, kept as data rather than hardcoded in MovementMotor (Section 5.5, §8).
    [Export] public float JumpStaminaCost { get; set; } = 15.0f;
    [Export] public float SprintStaminaDrainPerSecond { get; set; } = 25.0f;
    [Export] public float StaminaRegenPerSecond { get; set; } = 15.0f;
    [Export] public float AimSpeedMultiplier { get; set; } = 0.6f;

    /// <summary>Baseline move_speed stat the WalkSpeed/RunSpeed are authored against; the StatBlock ratio scales off this.</summary>
    [Export] public float BaseMoveSpeed { get; set; } = 5.0f;
}
