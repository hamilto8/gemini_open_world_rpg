using Godot;
using Meridian.Core;

namespace Meridian.Player;

/// <summary>
/// Interaction query component. Casts a short ray from the player's chest along the camera's
/// horizontal facing and focuses the nearest <see cref="IInteractable"/>. The manual space query
/// (rather than the built-in RayCast3D ray) keeps it independent of the SpringArm3D that repositions
/// the camera rig's children. Enforces Section 14.1 requirements.
/// </summary>
public partial class Interactor : RayCast3D
{
    /// <summary>How far in front of the player (metres) an interactable can be reached.</summary>
    [Export] public float Reach { get; set; } = 3.0f;

    /// <summary>Ray origin height above the player's feet (chest level).</summary>
    [Export] public float ChestHeight { get; set; } = 1.2f;

    private Node3D? _player;
    private IInteractable? _focusedInteractable;

    public IInteractable? FocusedInteractable => _focusedInteractable;

    public override void _Ready()
    {
        // The rig (parent) is repositioned by the SpringArm; use the actual player body for the origin.
        _player = GetParentOrNull<Node3D>()?.GetParentOrNull<Node3D>();
        Enabled = false; // we run our own query in _PhysicsProcess
    }

    public override void _PhysicsProcess(double delta)
    {
        IInteractable? found = QueryInteractable();

        if (found != _focusedInteractable)
        {
            _focusedInteractable = found;
            if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
            {
                eventBus.Publish(new InteractionFocusChangedEvent(_focusedInteractable?.ActionPrompt, _focusedInteractable?.ObjectName));
            }
        }
    }

    private IInteractable? QueryInteractable()
    {
        if (_player == null) return null;

        Camera3D? camera = GetViewport().GetCamera3D();
        Vector3 facing = camera != null ? -camera.GlobalTransform.Basis.Z : -_player.GlobalTransform.Basis.Z;
        facing.Y = 0f;
        if (facing.LengthSquared() < 0.0001f) return null;
        facing = facing.Normalized();

        Vector3 origin = _player.GlobalPosition + new Vector3(0f, ChestHeight, 0f);
        Vector3 end = origin + facing * Reach;

        var query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollideWithAreas = true;
        query.CollideWithBodies = true;
        if (_player is CollisionObject3D playerBody)
        {
            query.Exclude = new Godot.Collections.Array<Rid> { playerBody.GetRid() };
        }

        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (result.Count == 0) return null;

        var collider = result["collider"].As<Node>();
        if (collider is IInteractable interactable && interactable.CanInteract(_player))
        {
            return interactable;
        }
        if (collider is Node node && node.GetParentOrNull<Node>() is IInteractable parentInteractable
            && parentInteractable.CanInteract(_player))
        {
            return parentInteractable;
        }
        return null;
    }

    public void TryInteract()
    {
        if (_player == null || _focusedInteractable == null) return;

        GD.Print($"[Interactor] Interacting with: {_focusedInteractable.ObjectName}");
        _focusedInteractable.Interact(_player);
    }

    /// <summary>
    /// Clears the current focus and notifies HUD listeners. Possession handoffs disable the on-foot
    /// avatar before another physics query can run, so they must explicitly clear the prompt.
    /// </summary>
    public void ClearFocus()
    {
        if (_focusedInteractable == null)
        {
            return;
        }

        _focusedInteractable = null;
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new InteractionFocusChangedEvent(null, null));
        }
    }
}

/// <summary>
/// Event broadcast when the focused interactable in front of the player changes.
/// </summary>
public record struct InteractionFocusChangedEvent(string? ActionPrompt, string? ObjectName);
