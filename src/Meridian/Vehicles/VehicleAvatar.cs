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

    [Export] public HandlingProfile? ProfileResource { get; set; }
    [Export] public float MaxFuel { get; set; } = 100f;
    [Export] public float InitialFuel { get; set; } = 80f;
    [Export] public Vector3 ExitOffset { get; set; } = new Vector3(-2.0f, 0.5f, 0.0f); // Spawns player on driver side

    private IVehicleHandlingProfile? _profile;
    private VehicleMotorModel? _motor;
    private float _initialFuelOverride = -1f;
    private IPlayerController? _possessingController;
    private Node3D? _boardedAvatar;
    private InputFrame _lastInput;

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
        InitializeMotor();
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

            // Track boarding avatar, hide it, and possess the vehicle.
            _boardedAvatar = avatarNode;
            _boardedAvatar.Visible = false;
            _boardedAvatar.ProcessMode = ProcessModeEnum.Disabled;

            pc.Possess(this);

            // Switch input context to Vehicle.
            if (Services.TryGet<IInputContextService>(out var inputService) && inputService != null)
            {
                inputService.PushContext(InputContextType.Vehicle);
            }
        }
    }

    public void OnPossessed(IPlayerController controller)
    {
        _possessingController = controller;
        _lastPublishedSpeed = float.NaN;
        _lastPublishedFuel = float.NaN;
        GD.Print("[VehicleAvatar] Possessed by controller.");
    }

    public void OnReleased()
    {
        _possessingController = null;
        _lastInput = default;
        GD.Print("[VehicleAvatar] Released by controller.");
    }

    public void ReceiveFrameInput(InputFrame input)
    {
        _lastInput = input;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_possessingController == null) return;

        // Exit request (Interact while possessing).
        if (_lastInput.InteractPressed)
        {
            Unboard();
            return;
        }

        if (_motor == null) return;

        // Throttle follows the shared forward-is-positive convention (see VehicleInput). Steering
        // maps MoveX (right positive). Brake is a held input, distinct from the edge-triggered jump.
        var motorInput = new VehicleInput(
            Throttle: _lastInput.MoveY,
            Steer: _lastInput.MoveX,
            Brake: _lastInput.BrakeHeld);

        _motor.Step(motorInput, (float)delta);

        // Integrate as a physics body: forward motion from the motor, gravity when airborne.
        Vector3 forward = -GlobalTransform.Basis.Z;
        Vector3 velocity = forward * _motor.Speed;

        if (!IsOnFloor())
        {
            velocity += GetGravity() * (float)delta;
        }
        else if (velocity.Y < 0f)
        {
            velocity.Y = 0f;
        }
        else
        {
            velocity.Y = Velocity.Y;
        }

        Velocity = velocity;
        MoveAndSlide();

        PublishStatsIfChanged();
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
        if (_possessingController == null || _boardedAvatar is not IPossessable avatarPossessable) return;

        GD.Print("[VehicleAvatar] Unboarding avatar...");

        // Restore avatar position next to the vehicle door.
        _boardedAvatar.GlobalPosition = GlobalPosition + GlobalTransform.Basis * ExitOffset;
        _boardedAvatar.Visible = true;
        _boardedAvatar.ProcessMode = ProcessModeEnum.Inherit;

        var controller = _possessingController;

        // Pop input context back to OnFoot.
        if (Services.TryGet<IInputContextService>(out var inputService) && inputService != null)
        {
            inputService.PopContext();
        }

        controller.Possess(avatarPossessable);
        _boardedAvatar = null;
    }
}

public record struct VehicleSpeedChangedEvent(float Speed);
public record struct VehicleFuelChangedEvent(float Fuel);
