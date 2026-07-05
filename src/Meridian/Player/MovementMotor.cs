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
    private StatBlock? _stats;

    public override void _Ready()
    {
        _body = GetParentOrNull<CharacterBody3D>();
        if (_body == null)
        {
            GD.PrintErr("[MovementMotor] Parent node is not a CharacterBody3D!");
        }

        _stats = GetParent().GetNodeOrNull<StatBlock>("StatBlock");
    }

    /// <summary>
    /// Processes physical locomotion velocity integration.
    /// </summary>
    public void Move(InputFrame input, LocomotionState state, bool isAiming, double delta)
    {
        if (_body == null) return;
        if (Profile == null) return;

        Vector3 velocity = _body.Velocity;

        // 1. Apply Gravity
        if (!_body.IsOnFloor())
        {
            float gravity = (float)PhysicsServer3D.AreaGetParam(
                _body.GetViewport().FindWorld3D().Space,
                PhysicsServer3D.AreaParameter.Gravity
            );
            velocity.Y -= gravity * Profile.GravityMultiplier * (float)delta;
        }

        // 2. Apply Jump force
        if (input.JumpPressed && _body.IsOnFloor() && state != LocomotionState.Crouch)
        {
            velocity.Y = Profile.JumpVelocity;
            
            // Subtract jump stamina cost if stamina is managed
            if (_stats != null)
            {
                float stamina = _stats.GetStat("stamina");
                _stats.SetBaseStat("stamina", Math.Max(0f, stamina - 15f));
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
            _stats.SetBaseStat("stamina", Math.Max(0f, stamina - (float)(25f * delta)));
        }
        // Stamina recovery when not sprinting
        else if (state != LocomotionState.Sprint && _stats != null)
        {
            float stamina = _stats.GetStat("stamina");
            float maxStamina = _stats.GetStat("max_stamina");
            if (stamina < maxStamina)
            {
                _stats.SetBaseStat("stamina", Math.Min(maxStamina, stamina + (float)(15f * delta)));
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

        // Aiming reduces movement speed by 40% (Section 5.3 aiming speed caps)
        if (isAiming)
        {
            baseSpeed *= 0.6f;
        }

        // Apply StatBlock modifiers to speed if available
        if (_stats != null)
        {
            float modSpeed = _stats.GetStat("move_speed");
            // If modified speed deviates from default (5.0), apply the ratio to the current speed
            float ratio = modSpeed / 5.0f;
            baseSpeed *= ratio;
        }

        return baseSpeed;
    }
}
