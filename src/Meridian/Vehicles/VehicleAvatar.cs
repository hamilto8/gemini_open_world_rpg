using Godot;
using Meridian.Core;
using Meridian.Data;
using Meridian.Input;

namespace Meridian.Vehicles;

/// <summary>
/// Vehicle body implementing possession and interaction. A thin Node that drives the engine-free
/// <see cref="VehicleMotorModel"/> each physics frame, then integrates the result as a physics body
/// (gravity + MoveAndSlide). Enforces Section 11 requirements.
/// </summary>
public partial class VehicleAvatar : CharacterBody3D, IPossessable, IInteractable
{
    // HUD data changes slowly; only republish when it moves by at least these thresholds.
    private const float SpeedEventEpsilon = 0.25f;
    private const float FuelEventEpsilon = 0.5f;

    /// <summary>Seconds the exit input (E / B) must be held to leave the vehicle.</summary>
    private const float ExitHoldSeconds = 0.6f;

    [Export] public HandlingProfile? ProfileResource { get; set; }
    [Export] public float MaxFuel { get; set; } = 100f;
    [Export] public float InitialFuel { get; set; } = 80f;
    [Export] public Vector3 ExitOffset { get; set; } = new Vector3(-2.0f, 0.5f, 0.0f); // Spawns player on driver side
    [Export] public float MouseLookSensitivity { get; set; } = 0.003f;
    [Export] public float StickLookSensitivity { get; set; } = 2.5f;

    private IVehicleHandlingProfile? _profile;
    private VehicleMotorModel? _motor;
    private float _initialFuelOverride = -1f;
    private IPlayerController? _possessingController;
    private Node3D? _boardedAvatar;
    private InputFrame _lastInput;
    private float _exitHoldTime;
    private bool _exitArmed;
    private Camera3D? _camera;
    private float _cameraYaw;
    private float _cameraPitch;

    private float _lastPublishedSpeed = float.NaN;
    private float _lastPublishedFuel = float.NaN;

    public string ObjectName => "Offroad Vehicle";
    public string ActionPrompt => "Board Vehicle";

    public float CurrentFuel => _motor?.Fuel ?? (_initialFuelOverride >= 0f ? _initialFuelOverride : InitialFuel);
    public float CurrentSpeed => _motor?.Speed ?? 0f;
    public float SteeringAngle => _motor?.SteeringAngle ?? 0f;
    public IVehicleHandlingProfile? Profile => _profile ?? ProfileResource;

    public override void _Ready()
    {
        // Fall back to a sensible default handling profile so a vehicle placed in a scene without an
        // authored HandlingProfile is still drivable (test world convenience).
        if (Profile == null)
        {
            _profile = new HandlingProfile
            {
                Id = "default_buggy",
                MaxSpeed = 22.0f,
                Acceleration = 12.0f,
                SteeringLimit = 35.0f,
                BrakingStrength = 24.0f,
                FuelBurnRate = 1.0f,
                Wheelbase = 2.6f,
                MaxLateralAcceleration = 9.0f,
            };
        }
        InitializeMotor();
        _camera = GetNodeOrNull<Camera3D>("Camera3D");
        if (_camera != null)
        {
            _cameraYaw = _camera.Rotation.Y;
            _cameraPitch = _camera.Rotation.X;
        }
    }

    /// <summary>Injects a handling profile and starting fuel (used by streaming restore and tests).</summary>
    public void Initialize(IVehicleHandlingProfile profile, float fuel)
    {
        _profile = profile;
        _initialFuelOverride = fuel;
        InitializeMotor();
    }

    private void InitializeMotor()
    {
        var profile = Profile;
        if (profile == null)
        {
            _motor = null;
            return;
        }

        float startFuel = _initialFuelOverride >= 0f ? _initialFuelOverride : InitialFuel;
        _motor = new VehicleMotorModel(profile, startFuel, MaxFuel);
    }

    public bool CanInteract(Node3D interactor)
    {
        // Can board if not already possessed by a controller.
        return _possessingController == null;
    }

    public void Interact(Node3D interactor)
    {
        if (_possessingController != null) return;

        if (Services.TryGet<IPlayerController>(out var pc) && pc != null
            && pc.PossessedEntity is Node3D avatarNode)
        {
            GD.Print($"[VehicleAvatar] Avatar boarding vehicle '{ObjectName}'...");

            // Track boarding avatar, hide it, and possess the vehicle. The input-context switch is
            // handled in OnPossessed/OnReleased so it stays paired with possession.
            _boardedAvatar = avatarNode;
            _boardedAvatar.Visible = false;
            _boardedAvatar.ProcessMode = ProcessModeEnum.Disabled;

            pc.Possess(this);
        }
    }

    public void OnPossessed(IPlayerController controller)
    {
        _possessingController = controller;
        _lastPublishedSpeed = float.NaN;
        _lastPublishedFuel = float.NaN;

        // Don't let the E/B press that boarded immediately count toward the exit hold — require a
        // release first, then a fresh long-press to leave.
        _exitArmed = false;
        _exitHoldTime = 0f;

        // Couple the Vehicle input context to possession so a direct Possess() can't bypass it (V7).
        if (Services.TryGet<IInputContextService>(out var inputService) && inputService != null)
        {
            inputService.PushContext(InputContextType.Vehicle);
        }

        // Switch to the vehicle's chase camera while driving (the on-foot camera is disabled with the
        // hidden avatar); the avatar re-activates its own camera in PlayerAvatar.OnPossessed on exit.
        _camera?.MakeCurrent();

        GD.Print("[VehicleAvatar] Possessed by controller.");
    }

    public void OnReleased()
    {
        _possessingController = null;
        _lastInput = default;

        if (Services.TryGet<IInputContextService>(out var inputService) && inputService != null)
        {
            inputService.TryPopContext(InputContextType.Vehicle);
        }

        GD.Print("[VehicleAvatar] Released by controller.");
    }

    public void ReceiveFrameInput(InputFrame input)
    {
        _lastInput = input;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_possessingController == null) return;

        // Exit on a long-press of E / B (armed only once the input is released after boarding).
        if (!_lastInput.ExitVehicleHeld)
        {
            _exitArmed = true;
            _exitHoldTime = 0f;
        }
        else if (_exitArmed)
        {
            _exitHoldTime += (float)delta;
            if (_exitHoldTime >= ExitHoldSeconds)
            {
                Unboard();
                return;
            }
        }

        UpdateCameraLook((float)delta);

        if (_motor == null) return;

        // Throttle: W/S on keyboard, Right/Left triggers on gamepad (VehicleThrottle). Forward is
        // positive (see VehicleInput). Steering maps MoveX (right positive); brake is a held input.
        var motorInput = new VehicleInput(
            Throttle: _lastInput.VehicleThrottle,
            Steer: _lastInput.MoveX,
            Brake: _lastInput.BrakeHeld);

        _motor.Step(motorInput, (float)delta);

        // Turn the body first so its transform stays the single source of heading (collision-safe);
        // the forward vector below is read from the updated basis. Zero speed → zero yaw (§11.1).
        RotateY(_motor.YawRateRadians * (float)delta);

        // Integrate as a physics body: forward motion from the motor, gravity when airborne.
        Vector3 forward = -GlobalTransform.Basis.Z;
        Vector3 velocity = forward * _motor.Speed;
        velocity.Y = Velocity.Y;

        if (!IsOnFloor())
        {
            velocity += GetGravity() * (float)delta;
        }
        else if (velocity.Y < 0f)
        {
            velocity.Y = 0f;
        }

        Velocity = velocity;
        MoveAndSlide();

        PublishStatsIfChanged();
    }

    private void UpdateCameraLook(float delta)
    {
        if (_camera == null)
        {
            return;
        }

        _cameraYaw -= _lastInput.LookX * MouseLookSensitivity;
        _cameraYaw -= _lastInput.LookStickX * StickLookSensitivity * delta;

        float pitchDelta = _lastInput.LookY * MouseLookSensitivity
                         + _lastInput.LookStickY * StickLookSensitivity * delta;
        _cameraPitch = Mathf.Clamp(_cameraPitch - pitchDelta, -Mathf.Pi / 3f, Mathf.Pi / 6f);
        _camera.Rotation = new Vector3(_cameraPitch, _cameraYaw, 0f);
    }

    private void PublishStatsIfChanged()
    {
        if (_motor == null) return;
        if (!Services.TryGet<IEventBus>(out var eventBus) || eventBus == null) return;

        if (float.IsNaN(_lastPublishedSpeed) || Mathf.Abs(_motor.Speed - _lastPublishedSpeed) >= SpeedEventEpsilon)
        {
            _lastPublishedSpeed = _motor.Speed;
            eventBus.Publish(new VehicleSpeedChangedEvent(_motor.Speed));
        }

        if (float.IsNaN(_lastPublishedFuel) || Mathf.Abs(_motor.Fuel - _lastPublishedFuel) >= FuelEventEpsilon)
        {
            _lastPublishedFuel = _motor.Fuel;
            eventBus.Publish(new VehicleFuelChangedEvent(_motor.Fuel));
        }
    }

    private void Unboard()
    {
        if (_possessingController == null) return;

        GD.Print("[VehicleAvatar] Unboarding avatar...");

        var controller = _possessingController;
        var avatar = _boardedAvatar;
        _boardedAvatar = null;

        // Always restore the avatar body, regardless of how possession resolves below.
        if (avatar != null)
        {
            avatar.GlobalPosition = GlobalPosition + GlobalTransform.Basis * ExitOffset;
            avatar.Visible = true;
            avatar.ProcessMode = ProcessModeEnum.Inherit;
        }

        // Hand control back to the avatar. If the tracked body somehow isn't possessable, still release
        // the vehicle so the player is never permanently stranded (V7). Either path runs OnReleased,
        // which pops the Vehicle input context.
        if (avatar is IPossessable avatarPossessable)
        {
            controller.Possess(avatarPossessable);
        }
        else
        {
            controller.Release();
        }
    }
}

public record struct VehicleSpeedChangedEvent(float Speed);
public record struct VehicleFuelChangedEvent(float Fuel);
