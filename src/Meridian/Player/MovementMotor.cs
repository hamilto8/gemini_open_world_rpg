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
    /// Processes physical locomotion velocity integration. <paramref name="cameraYaw"/> is the
    /// horizontal look angle; movement input is interpreted relative to it so the player travels in
    /// the direction the camera is pointing (standard third-person camera-relative movement).
    /// </summary>
    public void Move(InputFrame input, LocomotionState state, bool isAiming, float cameraYaw, double delta)
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

        // 3. Resolve target speed from the analog input magnitude: keyboard is always full tilt (1.0),
        //    while the gamepad stick gives 0..1 for speed-proportional movement.
        float inputMagnitude = Mathf.Min(new Vector2(input.MoveX, input.MoveY).Length(), 1.0f);
        float speed = ResolveTargetSpeed(state, isAiming, inputMagnitude);

        // 4. Integrate horizontal acceleration and friction. Direction is unit length; the magnitude is
        //    carried entirely by `speed`, so partial stick tilt yields a slower walk in the same heading.
        Vector2 horizontalVelocity = new Vector2(velocity.X, velocity.Z);

        if (inputMagnitude > 0.001f)
        {
            // Rotate the input by the camera yaw (not the body's facing): forward = camera-forward,
            // right = camera-right, projected onto the ground plane.
            Vector3 localDir = new Vector3(input.MoveX, 0, -input.MoveY).Normalized();
            Vector3 direction = (new Basis(Vector3.Up, cameraYaw) * localDir).Normalized();
            Vector2 targetHorizontal = new Vector2(direction.X, direction.Z) * speed;
            horizontalVelocity = horizontalVelocity.Lerp(targetHorizontal, Profile.Acceleration * (float)delta);
        }
        else
        {
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

    private float ResolveTargetSpeed(LocomotionState state, bool isAiming, float inputMagnitude)
    {
        if (Profile == null) return 0f;

        inputMagnitude = Mathf.Clamp(inputMagnitude, 0f, 1f);

        // Two gaits: the analog stick alone tops out at a walk (full tilt = WalkSpeed). Running is a
        // deliberate modifier — the run button (R3 on gamepad / Shift on keyboard) drives the Sprint
        // state, raising the ceiling to RunSpeed. Speed within a gait is proportional to stick tilt;
        // keyboard is always full tilt.
        float gaitSpeed = state switch
        {
            LocomotionState.Crouch => Profile.CrouchSpeed,
            LocomotionState.Sprint => Profile.RunSpeed,
            _ => Profile.WalkSpeed,
        };
        float baseSpeed = gaitSpeed * inputMagnitude;

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
