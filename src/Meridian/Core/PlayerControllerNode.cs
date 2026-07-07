using Godot;
using System;
using Meridian.Core.Save;
using Meridian.Input;
using Meridian.Items;
using Meridian.World;

namespace Meridian.Core;

/// <summary>
/// Persistent Node representing the player's brain.
/// Processes input, maintains possession, and tracks character stats/identity (StatBlock, inventory,
/// progression — doc §5.1).
/// </summary>
public partial class PlayerControllerNode : Node, IPlayerController, IInventoryProvider, ISaveParticipant
{
    private IPossessable? _possessedEntity;
    private readonly InventoryModel _inventory = new();
    private Vector2 _pendingLook;
    private bool _mouseCaptured;

    public IPossessable? PossessedEntity => _possessedEntity;

    public InventoryModel Inventory => _inventory;
    public WeaponInstance? EquippedWeapon { get; set; }

    public string ParticipantId => "PlayerState";
    public int RestoreOrder => 100; // Restores last after streaming and world environments are ready
    public Type StateType => typeof(PlayerStateDto);

    public override void _EnterTree()
    {
        Services.Register<IPlayerController>(this);
        Services.Register<IInventoryProvider>(this);
    }

    public override void _Ready()
    {
        // Guarantee the mouse-look callback fires (camera look is read in _UnhandledInput).
        SetProcessUnhandledInput(true);

        if (Services.TryGet<ISaveService>(out var saveService) && saveService != null)
        {
            saveService.RegisterParticipant(this);
        }
    }

    public override void _ExitTree()
    {
        if (Services.TryGet<ISaveService>(out var saveService) && saveService != null)
        {
            saveService.UnregisterParticipant(this);
        }

        // Symmetric unregistration so a torn-down controller can't hand out a stale reference (L2).
        if (Services.TryGet<IPlayerController>(out var pc) && ReferenceEquals(pc, this))
        {
            Services.Unregister<IPlayerController>();
        }
        if (Services.TryGet<IInventoryProvider>(out var provider) && ReferenceEquals(provider, this))
        {
            Services.Unregister<IInventoryProvider>();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // The controller owns input reading, so mouse-look is gated by the active input context here
        // rather than polled raw inside the camera (M11). Accumulated delta is drained each frame.
        if (@event is InputEventMouseMotion motion
            && Services.TryGet<IInputContextService>(out var inputService)
            && inputService != null
            && inputService.IsActionAllowed("look"))
        {
            _pendingLook += motion.Relative;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateMouseCapture();

        if (_possessedEntity == null) return;

        // Compile input snapshot
        var inputFrame = CompileInputFrame();

        // Feed input to possessed entity
        _possessedEntity.ReceiveFrameInput(inputFrame);
    }

    /// <summary>
    /// The input layer owns <see cref="Godot.Input.MouseMode"/>: cursor is captured only while a
    /// gameplay context allows looking, so menus/dialogue free the cursor without fighting the camera (M11).
    /// </summary>
    private void UpdateMouseCapture()
    {
        if (!Services.TryGet<IInputContextService>(out var inputService) || inputService == null)
        {
            return;
        }

        bool shouldCapture = inputService.IsActionAllowed("look");
        if (shouldCapture == _mouseCaptured)
        {
            return;
        }

        _mouseCaptured = shouldCapture;
        Godot.Input.MouseMode = shouldCapture
            ? Godot.Input.MouseModeEnum.Captured
            : Godot.Input.MouseModeEnum.Visible;
    }

    public void Possess(IPossessable entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (_possessedEntity != null)
        {
            Release();
        }

        _possessedEntity = entity;
        _possessedEntity.OnPossessed(this);
        GD.Print($"[PlayerController] Possessed: {entity.GetType().Name}");
        PublishPossessionChanged();
    }

    public void Release()
    {
        if (_possessedEntity != null)
        {
            GD.Print($"[PlayerController] Releasing: {_possessedEntity.GetType().Name}");
            var old = _possessedEntity;
            _possessedEntity = null;
            old.OnReleased();
            PublishPossessionChanged();
        }
    }

    private void PublishPossessionChanged()
    {
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new PossessionChangedEvent(_possessedEntity));
        }
    }

    private InputFrame CompileInputFrame()
    {
        var inputService = Services.Get<IInputContextService>();
        var frame = new InputFrame();

        // Drain accumulated mouse-look into the frame so the camera reads it from InputFrame, not raw input.
        frame.LookX = _pendingLook.X;
        frame.LookY = _pendingLook.Y;
        _pendingLook = Vector2.Zero;

        // Movement is analog (GetActionStrength) so the gamepad left stick works alongside WASD keys.
        frame.MoveY = Axis(inputService, "move_forward", "move_backward");
        frame.MoveX = Axis(inputService, "move_right", "move_left");

        // Right-stick camera look (analog rate; mouse-look above is a pixel delta).
        if (inputService.IsActionAllowed("look"))
        {
            frame.LookStickX = Axis(inputService, "look_right", "look_left");
            frame.LookStickY = Axis(inputService, "look_down", "look_up");
        }

        // Mouse look/aim delta tracking
        if (inputService.IsActionAllowed("aim"))
        {
            frame.AimHeld = Godot.Input.IsActionPressed("aim");
        }

        if (inputService.IsActionAllowed("fire"))
        {
            frame.FirePressed = Godot.Input.IsActionJustPressed("fire");
        }

        if (inputService.IsActionAllowed("reload"))
        {
            frame.ReloadPressed = Godot.Input.IsActionJustPressed("reload");
        }

        if (inputService.IsActionAllowed("jump"))
        {
            frame.JumpPressed = Godot.Input.IsActionJustPressed("jump");
        }

        if (inputService.IsActionAllowed("sprint"))
        {
            frame.SprintHeld = Godot.Input.IsActionPressed("sprint");
        }

        if (inputService.IsActionAllowed("crouch"))
        {
            frame.CrouchHeld = Godot.Input.IsActionPressed("crouch");
        }

        if (inputService.IsActionAllowed("brake"))
        {
            frame.BrakeHeld = Godot.Input.IsActionPressed("brake");
        }

        if (inputService.IsActionAllowed("interact"))
        {
            frame.InteractPressed = Godot.Input.IsActionJustPressed("interact");
        }

        return frame;
    }

    /// <summary>
    /// Analog axis value from a positive/negative action pair, gated by the active input context.
    /// Returns 1.0 for a pressed key or the analog magnitude for a gamepad stick.
    /// </summary>
    private static float Axis(IInputContextService inputService, string positive, string negative)
    {
        float pos = inputService.IsActionAllowed(positive) ? Godot.Input.GetActionStrength(positive) : 0f;
        float neg = inputService.IsActionAllowed(negative) ? Godot.Input.GetActionStrength(negative) : 0f;
        return pos - neg;
    }

    public object CaptureState()
    {
        float posX = 0f, posY = 0f, posZ = 0f;
        float rotY = 0f;

        if (_possessedEntity is Node3D spatialNode)
        {
            var globalPos = spatialNode.GlobalPosition;
            posX = globalPos.X;
            posY = globalPos.Y;
            posZ = globalPos.Z;
            rotY = spatialNode.GlobalRotation.Y;
        }

        // Pull live health/stamina from the possessed avatar's StatBlock, and the region from the streamer.
        var stats = GetPossessedStatBlock();
        float health = stats?.GetStat("health") ?? 100f;
        float stamina = stats?.GetStat("stamina") ?? 100f;

        string regionId = "unknown";
        if (Services.TryGet<IWorldStreamer>(out var streamer) && streamer?.CurrentRegionId is string id)
        {
            regionId = id;
        }

        return new PlayerStateDto(
            CurrentRegionId: regionId,
            PositionX: posX,
            PositionY: posY,
            PositionZ: posZ,
            RotationY: rotY,
            Health: health,
            Stamina: stamina,
            PossessedGuid: _possessedEntity is Node node ? node.Name : ""
        );
    }

    public void RestoreState(object stateDto)
    {
        if (stateDto is not PlayerStateDto dto)
        {
            return;
        }

        if (_possessedEntity is Node3D spatialNode)
        {
            spatialNode.GlobalPosition = new Vector3(dto.PositionX, dto.PositionY, dto.PositionZ);
            spatialNode.GlobalRotation = new Vector3(spatialNode.GlobalRotation.X, dto.RotationY, spatialNode.GlobalRotation.Z);
        }

        // Restore health/stamina symmetrically onto the avatar's StatBlock.
        var stats = GetPossessedStatBlock();
        if (stats != null)
        {
            stats.SetBaseStat("health", dto.Health);
            stats.SetBaseStat("stamina", dto.Stamina);
        }
    }

    private StatBlockNode? GetPossessedStatBlock()
    {
        return _possessedEntity is Node node ? node.GetNodeOrNull<StatBlockNode>("StatBlock") : null;
    }
}
