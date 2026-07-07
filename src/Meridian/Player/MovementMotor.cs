using Godot;
using System;
using Meridian.Core;
using Meridian.Data;

namespace Meridian.Player;

/// <summary>
/// Physics movement engine. Computes friction, acceleration, gravity, and slopes.
/// Enforces Section 5.2 requirements.
/// </summary>
public partial class MovementMotor : Node
{
    [Export] public MovementProfile? Profile { get; set; }

    private CharacterBody3D? _body;
    private StatBlockNode? _stats;

    public override void _Ready()
    {
        _body = GetParentOrNull<CharacterBody3D>();
        if (_body == null)
        {
            GD.PrintErr("[MovementMotor] Parent node is not a CharacterBody3D!");
        }

        _stats = GetParent().GetNodeOrNull<StatBlockNode>("StatBlock");
    }

    /// <summary>
    /// Processes physical locomotion velocity integration.
    /// </summary>
    public void Move(InputFrame input, LocomotionState state, bool isAiming, double delta)
    {
        if (_body == null) return;
        if (Profile == null) return;

        Vector3 velocity = _body.Velocity;

        // 1. Apply Gravity. CharacterBody3D.GetGravity() returns the acceleration vector (Godot 4.3+),
        //    which also respects gravity-override Areas — unlike the previous AreaGetParam(space, ...) misuse.
        if (!_body.IsOnFloor())
        {
            velocity += _body.GetGravity() * Profile.GravityMultiplier * (float)delta;
        }

        // 2. Apply Jump force
        if (input.JumpPressed && _body.IsOnFloor() && state != LocomotionState.Crouch)
        {
            velocity.Y = Profile.JumpVelocity;

            // Subtract jump stamina cost if stamina is managed
            if (_stats != null)
            {
                float stamina = _stats.GetStat("stamina");
                _stats.SetBaseStat("stamina", Math.Max(0f, stamina - Profile.JumpStaminaCost));
            }
        }

        // 3. Resolve Speeds (influenced by StatBlock speed modifier)
        float speed = ResolveTargetSpeed(state, isAiming);

        // Get movement input direction relative to body rotation
        Vector3 inputDir = new Vector3(input.MoveX, 0, -input.MoveY).Normalized();
        Vector3 direction = (_body.GlobalTransform.Basis * inputDir).Normalized();

        // 4. Integrate horizontal acceleration and friction
        Vector2 horizontalVelocity = new Vector2(velocity.X, velocity.Z);
        Vector2 targetHorizontal = new Vector2(direction.X, direction.Z) * speed;

        if (direction.LengthSquared() > 0.01f)
        {
            // Accelerate
            horizontalVelocity = horizontalVelocity.Lerp(targetHorizontal, Profile.Acceleration * (float)delta);
        }
        else
        {
            // Decelerate / Friction
            horizontalVelocity = horizontalVelocity.Lerp(Vector2.Zero, Profile.Friction * (float)delta);
        }

        velocity.X = horizontalVelocity.X;
        velocity.Z = horizontalVelocity.Y;

        // Apply calculated velocities to the body
        _body.Velocity = velocity;
        _body.MoveAndSlide();

        // Stamina drain during sprint (Section 5.3)
        if (state == LocomotionState.Sprint && _stats != null)
        {
            float stamina = _stats.GetStat("stamina");
            _stats.SetBaseStat("stamina", Math.Max(0f, stamina - (float)(Profile.SprintStaminaDrainPerSecond * delta)));
        }
        // Stamina recovery when not sprinting
        else if (state != LocomotionState.Sprint && _stats != null)
        {
            float stamina = _stats.GetStat("stamina");
            float maxStamina = _stats.GetStat("max_stamina");
            if (stamina < maxStamina)
            {
                _stats.SetBaseStat("stamina", Math.Min(maxStamina, stamina + (float)(Profile.StaminaRegenPerSecond * delta)));
            }
        }
    }

    private float ResolveTargetSpeed(LocomotionState state, bool isAiming)
    {
        if (Profile == null) return 0f;

        float baseSpeed = state switch
        {
            LocomotionState.Walk => Profile.WalkSpeed,
            LocomotionState.Sprint => Profile.SprintSpeed,
            LocomotionState.Crouch => Profile.CrouchSpeed,
            _ => Profile.RunSpeed
        };

        // Aiming reduces movement speed (Section 5.3 aiming speed caps)
        if (isAiming)
        {
            baseSpeed *= Profile.AimSpeedMultiplier;
        }

        // Apply StatBlock modifiers to speed if available
        if (_stats != null && Profile.BaseMoveSpeed > 0f)
        {
            float modSpeed = _stats.GetStat("move_speed");
            // Scale by how far the modified move_speed deviates from the authored baseline.
            float ratio = modSpeed / Profile.BaseMoveSpeed;
            baseSpeed *= ratio;
        }

        return baseSpeed;
    }
}
