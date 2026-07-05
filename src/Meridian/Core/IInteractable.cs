using Godot;

namespace Meridian.Core;

/// <summary>
/// Interface for any world prop that the player can interact with (loot boxes, doors, vehicle seats).
/// Enforces Section 14.1 requirements.
/// </summary>
public interface IInteractable
{
    string ActionPrompt { get; }
    string ObjectName { get; }

    bool CanInteract(Node3D interactor);
    void Interact(Node3D interactor);
}
