using Godot;
using System;
using Meridian.Core;
using Meridian.Input;
using Meridian.UI;

namespace Meridian.Player;

/// <summary>
/// Scene-glue script for the PlayerAvatar (on-foot body).
/// Implements IPossessable to receive and execute controller inputs.
/// Enforces Section 5.2 composition rules.
/// </summary>
public partial class PlayerAvatar : CharacterBody3D, IPossessable
{
    private StatBlockNode? _stats;
    private MovementMotor? _motor;
    private CameraRig? _cameraRig;
    private Interactor? _interactor;
    private MinimalHud? _hud;

    private readonly LocomotionStateMachine _hsm = new();
    private InputFrame _lastInput;

    public override void _Ready()
    {
        _stats = GetNodeOrNull<StatBlockNode>("StatBlock");
        _motor = GetNodeOrNull<MovementMotor>("MovementMotor");
        _cameraRig = GetNodeOrNull<CameraRig>("CameraRig");
        _interactor = GetNodeOrNull<Interactor>("CameraRig/Interactor");

        // Automatically bind or find HUD in UILayer
        var root = GetTree().CurrentScene;
        _hud = root.GetNodeOrNull<MinimalHud>("UILayer/MinimalHud");

        // Auto possess avatar at boot if controller is ready (GDD possession workflow)
        CallDeferred(nameof(AutoPossess));
    }

    private void AutoPossess()
    {
        if (Services.TryGet<IPlayerController>(out var controller) && controller != null)
        {
            controller.Possess(this);
        }
    }

    public void OnPossessed(PlayerControllerNode controller)
    {
        GD.Print("[PlayerAvatar] Possessed by controller.");
    }

    public void OnReleased()
    {
        GD.Print("[PlayerAvatar] Released by controller.");
        _lastInput = default;
    }

    public void ReceiveFrameInput(InputFrame input)
    {
        _lastInput = input;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_motor == null || _stats == null) return;

        float stamina = _stats.GetStat("stamina");
        float maxStamina = _stats.GetStat("max_stamina");
        float health = _stats.GetStat("health");
        float maxHealth = _stats.GetStat("max_health");

        // 1. Update Locomotion HSM
        _hsm.Update(
            input: _lastInput,
            isOnFloor: IsOnFloor(),
            velocityVertical: Velocity.Y,
            velocityHorizontalLength: new Vector3(Velocity.X, 0, Velocity.Z).Length(),
            currentStamina: stamina
        );

        // 2. Perform Movement Motor integration
        _motor.Move(_lastInput, _hsm.CurrentState, _hsm.Aiming, delta);

        // 3. Update Camera positioning and smoothing
        _cameraRig?.UpdateCamera(_hsm.Aiming, delta);

        // 4. Update HUD elements (event-free or basic updates)
        _hud?.UpdatePlayerStats(health, maxHealth, stamina, maxStamina);
        _hud?.SetAiming(_hsm.Aiming);

        // 5. Execute interaction input
        if (_lastInput.InteractPressed)
        {
            _interactor?.TryInteract();
        }
    }
}
