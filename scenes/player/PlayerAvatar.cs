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

        // Drive HUD health/stamina from StatBlock changes rather than polling every physics frame (L11).
        if (_stats != null)
        {
            _stats.StatChanged += OnStatChanged;
            RefreshHudStats();
        }

        // Auto possess avatar at boot if controller is ready (GDD possession workflow)
        CallDeferred(nameof(AutoPossess));
    }

    private void OnStatChanged(string statId, float newValue)
    {
        // Only the vitals shown on the HUD warrant a refresh.
        if (statId is "health" or "max_health" or "stamina" or "max_stamina")
        {
            RefreshHudStats();
        }
    }

    private void RefreshHudStats()
    {
        if (_hud == null || _stats == null) return;
        _hud.UpdatePlayerStats(
            _stats.GetStat("health"), _stats.GetStat("max_health"),
            _stats.GetStat("stamina"), _stats.GetStat("max_stamina"));
    }

    private void AutoPossess()
    {
        if (Services.TryGet<IPlayerController>(out var controller) && controller != null)
        {
            controller.Possess(this);
        }
    }

    public void OnPossessed(IPlayerController controller)
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

        // 1. Update Locomotion HSM
        _hsm.Update(
            input: _lastInput,
            isOnFloor: IsOnFloor(),
            velocityVertical: Velocity.Y,
            velocityHorizontalLength: new Vector3(Velocity.X, 0, Velocity.Z).Length(),
            currentStamina: stamina
        );

        // 2. Update the camera first so movement uses this frame's look direction.
        _cameraRig?.UpdateCamera(_lastInput, _hsm.Aiming, delta);

        // 3. Perform Movement Motor integration, camera-relative (player moves where the camera points).
        float cameraYaw = _cameraRig?.Yaw ?? GlobalRotation.Y;
        _motor.Move(_lastInput, _hsm.CurrentState, _hsm.Aiming, cameraYaw, delta);

        // 4. HUD stats are driven by StatBlockNode.StatChanged (see _Ready); only the aim reticle,
        //    which mirrors transient HSM state, is pushed here.
        _hud?.SetAiming(_hsm.Aiming);

        // 5. Execute interaction input
        if (_lastInput.InteractPressed)
        {
            _interactor?.TryInteract();
        }

        // TODO(combat, V4): the avatar has no EquipmentHolder/WeaponController yet, so InputFrame.FirePressed
        // is compiled but never consumed and PlayerControllerNode.EquippedWeapon is never assigned. Wire up
        // §5.2 composition (EquipmentHolder) + §6.3 weapon runtime to make firing and UpgradeBench reachable
        // from play (the WeaponRuntime/DamagePipeline internals they'd use are already implemented and tested).
    }
}
