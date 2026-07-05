using Godot;
using System;
using Meridian.Core.Save;
using Meridian.Input;

namespace Meridian.Core;

/// <summary>
/// Persistent Node representing the player's brain.
/// Processes input, maintains possession, and tracks character stats/identity.
/// </summary>
public partial class PlayerControllerNode : Node, IPlayerController, ISaveParticipant
{
    private IPossessable? _possessedEntity;

    public IPossessable? PossessedEntity => _possessedEntity;

    public string ParticipantId => "PlayerState";
    public int RestoreOrder => 100; // Restores last after streaming and world environments are ready

    public override void _EnterTree()
    {
        Services.Register<IPlayerController>(this);
    }

    public override void _Ready()
    {
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
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_possessedEntity == null) return;

        // Compile input snapshot
        var inputFrame = CompileInputFrame();

        // Feed input to possessed entity
        _possessedEntity.ReceiveFrameInput(inputFrame);
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
    }

    public void Release()
    {
        if (_possessedEntity != null)
        {
            GD.Print($"[PlayerController] Releasing: {_possessedEntity.GetType().Name}");
            var old = _possessedEntity;
            _possessedEntity = null;
            old.OnReleased();
        }
    }

    private InputFrame CompileInputFrame()
    {
        var inputService = Services.Get<IInputContextService>();
        var frame = new InputFrame();

        // Standard actions allowed checks via input context mapping (Section 17.1)
        if (inputService.IsActionAllowed("move_forward") && Godot.Input.IsActionPressed("move_forward")) frame.MoveY += 1.0f;
        if (inputService.IsActionAllowed("move_backward") && Godot.Input.IsActionPressed("move_backward")) frame.MoveY -= 1.0f;
        if (inputService.IsActionAllowed("move_left") && Godot.Input.IsActionPressed("move_left")) frame.MoveX -= 1.0f;
        if (inputService.IsActionAllowed("move_right") && Godot.Input.IsActionPressed("move_right")) frame.MoveX += 1.0f;

        // Mouse look/aim delta tracking
        if (inputService.IsActionAllowed("aim"))
        {
            frame.AimHeld = Godot.Input.IsActionPressed("aim");
        }

        if (inputService.IsActionAllowed("fire"))
        {
            frame.FirePressed = Godot.Input.IsActionJustPressed("fire");
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

        if (inputService.IsActionAllowed("interact"))
        {
            frame.InteractPressed = Godot.Input.IsActionJustPressed("interact");
        }

        return frame;
    }

    public object CaptureState()
    {
        // Persist position and region.
        // For Phase 1, we pull position directly from the possessed entity if it supports 3D spatial properties, or default it.
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

        return new PlayerStateDto(
            CurrentRegionId: "harbor_town",
            PositionX: posX,
            PositionY: posY,
            PositionZ: posZ,
            RotationY: rotY,
            Health: 100f, // StatBlock value will override this
            Stamina: 100f,
            PossessedGuid: _possessedEntity != null ? "avatar_player" : ""
        );
    }

    public void RestoreState(object stateDto)
    {
        if (stateDto is PlayerStateDto dto)
        {
            if (_possessedEntity is Node3D spatialNode)
            {
                spatialNode.GlobalPosition = new Vector3(dto.PositionX, dto.PositionY, dto.PositionZ);
                spatialNode.GlobalRotation = new Vector3(spatialNode.GlobalRotation.X, dto.RotationY, spatialNode.GlobalRotation.Z);
            }
        }
    }
}
