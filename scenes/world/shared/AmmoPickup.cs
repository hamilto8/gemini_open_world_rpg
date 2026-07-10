using Godot;
using Meridian.Core;
using Meridian.Core.Registry;
using Meridian.Items;

namespace Meridian.Scenes.World;

/// <summary>World interactable that adds reserve ammo to the player's inventory on interact.</summary>
public partial class AmmoPickup : StaticBody3D, IInteractable
{
    [Export] public string AmmoTypeId { get; set; } = "ammo_9mm";
    [Export] public int Amount { get; set; } = 24;

    public string ObjectName => $"{AmmoTypeId} x{Amount}";
    public string ActionPrompt => "Pick up";

    public bool CanInteract(Node3D interactor) => true;

    public void Interact(Node3D interactor)
    {
        if (!Services.TryGet<IInventoryProvider>(out var provider) || provider == null) return;

        // Resolve the ammo item definition from the data-driven registry rather than constructing it inline.
        // A missing database or unknown id is a data error: warn loudly and leave the pickup in the world
        // (the ContentValidator catches it at authoring time). Never free the node on a data error (§19.1).
        if (!Services.TryGet<IContentDatabase>(out var content) || content == null)
        {
            GD.PushWarning("[AmmoPickup] No IContentDatabase registered; cannot resolve ammo definition.");
            return;
        }

        if (!content.Items.TryGet(AmmoTypeId, out var definition) || definition == null)
        {
            GD.PushWarning($"[AmmoPickup] Unknown ammo id '{AmmoTypeId}'; not in the Items registry.");
            return;
        }

        // Free the pickup only once the ammo actually landed in the inventory — a full pack must not
        // destroy the item (finding: pickups discarded items when AddItem failed).
        provider.Inventory.RegisterDefinition(definition);
        if (!provider.Inventory.AddItem(new ItemInstance(AmmoTypeId, Amount)))
        {
            WeaponPickup.PublishNotice($"Pack is full — can't carry {Amount}x {AmmoTypeId}.");
            return;
        }

        GD.Print($"[Pickup] +{Amount} {AmmoTypeId}.");
        QueueFree();
    }
}
