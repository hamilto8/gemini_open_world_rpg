using Godot;
using System;
using Meridian.Core;
using Meridian.Data;
using Meridian.Input;

namespace Meridian.Vehicles;

/// <summary>
/// Vehicle character avatar implementing possession and interaction.
/// Controls movement calculations, braking, steering limits, and fuel depletion.
/// Enforces Section 8.0 requirements.
/// </summary>
public partial class VehicleAvatar : CharacterBody3D, IPossessable, IInteractable
{
    [Export] public HandlingProfile? ProfileResource { get; set; }
    [Export] public float MaxFuel { get; set; } = 100f;
    [Export] public float InitialFuel { get; set; } = 80f;
    [Export] public Vector3 ExitOffset { get; set; } = new Vector3(-2.0f, 0.5f, 0.0f); // Spawns player on driver side

    private IVehicleHandlingProfile? _profile;
    private float _currentFuel;
    private PlayerControllerNode? _possessingController;
    private Node3D? _boardedAvatar;
    private float _currentSpeed = 0f;
    private float _steeringAngle = 0f;
    private InputFrame _lastInput;

    public string ObjectName => "Offroad Vehicle";
    public string ActionPrompt => "Board Vehicle";

    public float CurrentFuel => _currentFuel;
    public float CurrentSpeed => _currentSpeed;
    public float SteeringAngle => _steeringAngle;
    public IVehicleHandlingProfile? Profile => _profile ?? ProfileResource;

    public override void _Ready()
    {
        _currentFuel = InitialFuel;
    }

    public void Initialize(IVehicleHandlingProfile profile, float fuel)
    {
        _profile = profile;
        _currentFuel = fuel;
    }

    public bool CanInteract(Node3D interactor)
    {
        // Can board if not already possessed by a controller
        return _possessingController == null;
    }

    public void Interact(Node3D interactor)
    {
        if (_possessingController != null) return;

        // Try boarding from player avatar
        if (Services.TryGet<IPlayerController>(out var pc) && pc is PlayerControllerNode playerController)
        {
            var avatarNode = playerController.PossessedEntity as Node3D;
            if (avatarNode != null)
            {
                GD.Print($"[VehicleAvatar] Avatar boarding vehicle '{ObjectName}'...");
                
                // Track boarding avatar, hide it, and possess vehicle
                _boardedAvatar = avatarNode;
                _boardedAvatar.Visible = false;
                _boardedAvatar.ProcessMode = ProcessModeEnum.Disabled;

                playerController.Possess(this);

                // Switch input context to Vehicle
                if (Services.TryGet<IInputContextService>(out var inputService) && inputService != null)
                {
                    inputService.PushContext(InputContextType.Vehicle);
                }
            }
        }
    }

    public void OnPossessed(PlayerControllerNode controller)
    {
        _possessingController = controller;
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

        // Check if player requested unboard/exit (e.g. presses Interact button while possessed)
        if (_lastInput.InteractPressed)
        {
            Unboard();
            return;
        }

        if (Profile == null) return;

        // Simulate steering angle based on MoveX input (steering angle caps)
        _steeringAngle = _lastInput.MoveX * Profile.SteeringLimit;

        // Accelerate if fuel remains and throttle is pressed (MoveY < 0 is forward in GDD layout representation)
        float throttleInput = -_lastInput.MoveY;
        bool hasThrottle = throttleInput > 0.05f;

        if (hasThrottle && _currentFuel > 0f)
        {
            // Burn fuel
            float deltaFuel = Profile.FuelBurnRate * (float)delta;
            _currentFuel = Math.Max(0f, _currentFuel - deltaFuel);

            // Apply acceleration
            _currentSpeed = Math.Min(Profile.MaxSpeed, _currentSpeed + (Profile.Acceleration * (float)delta));
        }
        else
        {
            // Decelerate or apply friction deceleration
            float deceleration = Profile.BrakingStrength * 0.1f;
            _currentSpeed = Math.Max(0f, _currentSpeed - (deceleration * (float)delta));
        }

        // Apply braking if Space/Jump is held (BrakingStrength)
        if (_lastInput.JumpPressed)
        {
            _currentSpeed = Math.Max(0f, _currentSpeed - (Profile.BrakingStrength * (float)delta));
        }

        // Update velocity (Move forward along current facing direction)
        Vector3 forward = -GlobalTransform.Basis.Z;
        Velocity = forward * _currentSpeed;

        MoveAndSlide();

        // Broadcast stats
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new VehicleSpeedChangedEvent(_currentSpeed));
            eventBus.Publish(new VehicleFuelChangedEvent(_currentFuel));
        }
    }

    private void Unboard()
    {
        if (_possessingController == null || _boardedAvatar == null) return;

        GD.Print("[VehicleAvatar] Unboarding avatar...");
        
        // Restore avatar position next to the vehicle door
        _boardedAvatar.GlobalPosition = GlobalPosition + GlobalTransform.Basis * ExitOffset;
        _boardedAvatar.Visible = true;
        _boardedAvatar.ProcessMode = ProcessModeEnum.Inherit;

        var controller = _possessingController;
        
        // Pop input context back to OnFoot
        if (Services.TryGet<IInputContextService>(out var inputService) && inputService != null)
        {
            inputService.PopContext();
        }

        controller.Possess((IPossessable)_boardedAvatar);
        _boardedAvatar = null;
    }
}

public record struct VehicleSpeedChangedEvent(float Speed);
public record struct VehicleFuelChangedEvent(float Fuel);
