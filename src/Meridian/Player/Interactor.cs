using Godot;
using System;
using Meridian.Core;

namespace Meridian.Player;

/// <summary>
/// Interaction raycast/shape query component attached to the player character.
/// Detects IInteractable props and executes interaction actions.
/// Enforces Section 14.1 requirements.
/// </summary>
public partial class Interactor : RayCast3D
{
    private Node3D? _owner;
    private IInteractable? _focusedInteractable;

    public IInteractable? FocusedInteractable => _focusedInteractable;

    public override void _Ready()
    {
        _owner = GetParentOrNull<Node3D>();
        
        // Target raycast length (e.g. 2.5 meters)
        TargetPosition = new Vector3(0, 0, -2.5f);
        Enabled = true;
        CollideWithAreas = true;
        CollideWithBodies = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_owner == null) return;

        IInteractable? found = null;
        if (IsColliding())
        {
            var collider = GetCollider();
            if (collider is IInteractable interactable && interactable.CanInteract(_owner))
            {
                found = interactable;
            }
            // Check parent in case the collision shape is nested
            else if (collider is Node node && node.GetParent() is IInteractable parentInteractable && parentInteractable.CanInteract(_owner))
            {
                found = parentInteractable;
            }
        }

        if (found != _focusedInteractable)
        {
            _focusedInteractable = found;
            // Notify UI layer / broadcast interaction target change
            if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
            {
                eventBus.Publish(new InteractionFocusChangedEvent(_focusedInteractable?.ActionPrompt, _focusedInteractable?.ObjectName));
            }
        }
    }

    public void TryInteract()
    {
        if (_owner == null || _focusedInteractable == null) return;
        
        GD.Print($"[Interactor] Interacting with: {_focusedInteractable.ObjectName}");
        _focusedInteractable.Interact(_owner);
    }
}

/// <summary>
/// Event broadcast when the focused interactable in front of the player changes.
/// </summary>
public record struct InteractionFocusChangedEvent(string? ActionPrompt, string? ObjectName);
