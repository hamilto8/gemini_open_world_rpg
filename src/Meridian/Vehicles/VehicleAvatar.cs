using Godot;
using System;
using System.Collections.Generic;
using Meridian.Combat;
using Meridian.Core;
using Meridian.Core.Save;
using Meridian.Data;
using Meridian.Input;

namespace Meridian.Vehicles;

/// <summary>
/// Vehicle body implementing possession and interaction. A thin Node that drives the engine-free
/// <see cref="VehicleMotorModel"/> each physics frame, then integrates the result as a physics body
/// (gravity + MoveAndSlide). Enforces Section 11 requirements.
/// </summary>
public partial class VehicleAvatar : CharacterBody3D, IPossessable, IInteractable, IPersistentVehicle, IDamageable, IHitZoneResolver
{
    // HUD data changes slowly; only republish when it moves by at least these thresholds.
    private const float SpeedEventEpsilon = 0.25f;
    private const float FuelEventEpsilon = 0.5f;

    /// <summary>Seconds the exit input (E / B) must be held to leave the vehicle.</summary>
    private const float ExitHoldSeconds = 0.6f;

    [Export] public HandlingProfile? ProfileResource { get; set; }
    [Export] public string PersistentVehicleId { get; set; } = "vehicle";
    [Export] public float MaxFuel { get; set; } = 100f;
    [Export] public float InitialFuel { get; set; } = 80f;
    [Export] public float MaxHealth { get; set; } = 250f;
    [Export] public float Armor { get; set; } = 15f;
    [Export] public float RespawnSeconds { get; set; } = 10f;
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
    private readonly StatBlock _vitals = new();
    private Transform3D _spawnTransform;
    private uint _originalCollisionLayer;
    private float _respawnRemaining;
    private bool _isDestroyed;

    private float _lastPublishedSpeed = float.NaN;
    private float _lastPublishedFuel = float.NaN;

    public string ObjectName => "Offroad Vehicle";
    public string ActionPrompt => "Board Vehicle";

    public float CurrentFuel => _motor?.Fuel ?? (_initialFuelOverride >= 0f ? _initialFuelOverride : InitialFuel);
    public float CurrentSpeed => _motor?.Speed ?? 0f;
    public float SteeringAngle => _motor?.SteeringAngle ?? 0f;
    public IVehicleHandlingProfile? Profile => _profile ?? ProfileResource;
    public string VehicleDefinitionId => Profile?.Id ?? "unknown_vehicle";

    public override void _Ready()
    {
        _spawnTransform = GlobalTransform;
        _originalCollisionLayer = CollisionLayer;
        _vitals.SetBaseStat("max_health", MaxHealth);
        _vitals.SetBaseStat("health", MaxHealth);
        _vitals.SetBaseStat("armor", Armor);

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

        if (Services.TryGet<VehiclePersistenceService>(out var persistence) && persistence != null)
        {
            persistence.Register(this);
        }
    }

    public override void _ExitTree()
    {
        if (Services.TryGet<VehiclePersistenceService>(out var persistence) && persistence != null)
        {
            persistence.Unregister(this);
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
        if (_isDestroyed)
        {
            if (RespawnSeconds > 0f)
            {
                _respawnRemaining -= (float)delta;
                if (_respawnRemaining <= 0f)
                {
                    RespawnVehicle();
                }
            }
            return;
        }

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
            avatar.GlobalPosition = FindSafeExitPosition(avatar);
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

    private Vector3 FindSafeExitPosition(Node3D avatar)
    {
        Vector3[] localOffsets =
        {
            ExitOffset,
            new Vector3(-ExitOffset.X, ExitOffset.Y, ExitOffset.Z),
            new Vector3(0f, ExitOffset.Y, Math.Abs(ExitOffset.X)),
            new Vector3(0f, ExitOffset.Y, -Math.Abs(ExitOffset.X)),
        };

        PhysicsDirectSpaceState3D space = GetWorld3D().DirectSpaceState;
        foreach (Vector3 offset in localOffsets)
        {
            Vector3 approximate = GlobalPosition + GlobalTransform.Basis * offset;
            var groundQuery = PhysicsRayQueryParameters3D.Create(
                approximate + Vector3.Up * 2f,
                approximate + Vector3.Down * 4f);
            groundQuery.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

            Godot.Collections.Dictionary hit = space.IntersectRay(groundQuery);
            Vector3 candidate = hit.Count > 0 ? (Vector3)hit["position"] + Vector3.Up * 0.08f : approximate;
            if (IsExitSpaceClear(space, avatar, candidate))
            {
                return candidate;
            }
        }

        GD.PushWarning($"[VehicleAvatar] No fully clear exit found for '{PersistentVehicleId}'; using elevated fallback.");
        return GlobalPosition + GlobalTransform.Basis * ExitOffset + Vector3.Up;
    }

    private bool IsExitSpaceClear(PhysicsDirectSpaceState3D space, Node3D avatar, Vector3 candidate)
    {
        if (avatar.GetNodeOrNull<CollisionShape3D>("CollisionShape3D")?.Shape is not Shape3D shape)
        {
            return true;
        }

        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new Transform3D(avatar.GlobalBasis, candidate),
            CollisionMask = CollisionMask,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Exclude = new Godot.Collections.Array<Rid> { GetRid() },
        };
        return space.IntersectShape(query, 1).Count == 0;
    }

    public HitZone ResolveHitZone(Vector3 worldHitPosition)
        => worldHitPosition.Y - GlobalPosition.Y > 0.8f ? HitZone.Weakpoint : HitZone.Body;

    public void ApplyDamage(DamageInfo info)
    {
        if (_isDestroyed) return;

        DamageApplicationResult result = DamagePipeline.Apply(_vitals, info);
        if (!result.WasApplied) return;

        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new DamageDealtEvent(
                ObjectName,
                result.AppliedDamage,
                info.Zone is HitZone.Head or HitZone.Weakpoint,
                result.NewHealth,
                result.IsDead));
        }

        if (result.IsDead)
        {
            DestroyVehicle();
        }
    }

    private void DestroyVehicle()
    {
        if (_possessingController != null)
        {
            Unboard();
        }
        _isDestroyed = true;
        _respawnRemaining = RespawnSeconds;
        Velocity = Vector3.Zero;
        Visible = false;
        CollisionLayer = 0;
    }

    private void RespawnVehicle()
    {
        GlobalTransform = _spawnTransform;
        Velocity = Vector3.Zero;
        _vitals.SetBaseStat("health", _vitals.GetStat("max_health"));
        _initialFuelOverride = InitialFuel;
        InitializeMotor();
        Visible = true;
        CollisionLayer = _originalCollisionLayer;
        _isDestroyed = false;
        _respawnRemaining = 0f;
    }

    public VehicleStateDto CaptureVehicleState(string currentRegionId, bool isPlayerPossessed)
    {
        return new VehicleStateDto(
            PersistentId: PersistentVehicleId,
            DefinitionId: VehicleDefinitionId,
            RegionId: currentRegionId,
            PositionX: GlobalPosition.X,
            PositionY: GlobalPosition.Y,
            PositionZ: GlobalPosition.Z,
            RotationY: GlobalRotation.Y,
            Fuel: CurrentFuel,
            Health: _vitals.GetStat("health"),
            IsPlayerPossessed: isPlayerPossessed,
            CustomState: new Dictionary<string, string>());
    }

    public void RestoreVehicleState(VehicleStateDto state)
    {
        if (!state.PersistentId.Equals(PersistentVehicleId, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        GlobalPosition = new Vector3(state.PositionX, state.PositionY, state.PositionZ);
        GlobalRotation = new Vector3(GlobalRotation.X, state.RotationY, GlobalRotation.Z);
        _initialFuelOverride = state.Fuel;
        InitializeMotor();
        _vitals.SetBaseStat("health", Math.Clamp(state.Health, 0f, _vitals.GetStat("max_health")));
        _isDestroyed = state.Health <= 0f;
        Visible = !_isDestroyed;
        CollisionLayer = _isDestroyed ? 0 : _originalCollisionLayer;
        _lastPublishedFuel = float.NaN;
        _lastPublishedSpeed = float.NaN;
    }
}

public record struct VehicleSpeedChangedEvent(float Speed);
public record struct VehicleFuelChangedEvent(float Fuel);
