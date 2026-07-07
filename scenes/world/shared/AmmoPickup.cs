using Godot;
using Meridian.Core;
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
        if (Services.TryGet<IInventoryProvider>(out var provider) && provider != null)
        {
            provider.Inventory.RegisterDefinition(WeaponPickup.MakeAmmoDefinition(AmmoTypeId));
            provider.Inventory.AddItem(new ItemInstance(AmmoTypeId, Amount));
            GD.Print($"[Pickup] +{Amount} {AmmoTypeId}.");
        }
        QueueFree();
    }
}
