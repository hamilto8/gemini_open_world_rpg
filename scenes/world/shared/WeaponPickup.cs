using Godot;
using Meridian.Combat;
using Meridian.Core;
using Meridian.Data;
using Meridian.Items;

namespace Meridian.Scenes.World;

/// <summary>
/// World interactable that equips a weapon (and stocks starting reserve ammo) on interact.
/// Builds a <see cref="WeaponResource"/> from exported fields so the test world needs no authored .tres.
/// </summary>
public partial class WeaponPickup : StaticBody3D, IInteractable
{
    [Export] public string WeaponId { get; set; } = "test_pistol";
    [Export] public string WeaponName { get; set; } = "Test Pistol";
    [Export] public float BaseDamage { get; set; } = 25f;
    [Export] public float FireRate { get; set; } = 6f;
    [Export] public float MaxRange { get; set; } = 120f;
    [Export] public string AmmoTypeId { get; set; } = "ammo_9mm";
    [Export] public int MagazineSize { get; set; } = 12;
    [Export] public float ReloadTime { get; set; } = 1.5f;
    [Export] public int StartingReserveAmmo { get; set; } = 48;

    public string ObjectName => WeaponName;
    public string ActionPrompt => "Pick up";

    public bool CanInteract(Node3D interactor) => true;

    public void Interact(Node3D interactor)
    {
        if (!Services.TryGet<IPlayerController>(out var pc) || pc?.PossessedEntity is not IWeaponHolder holder)
        {
            GD.PushWarning("[WeaponPickup] No weapon holder is possessed.");
            return;
        }

        var definition = new WeaponResource
        {
            Id = WeaponId,
            DisplayName = WeaponName,
            BaseDamage = BaseDamage,
            FireRate = FireRate,
            MaxRange = MaxRange,
            AmmoTypeId = AmmoTypeId,
            MagazineSize = MagazineSize,
            ReloadTime = ReloadTime,
        };

        Services.TryGet<IInventoryProvider>(out var provider);

        // Register the ammo item type and stock some reserves so reloading works.
        if (provider != null)
        {
            provider.Inventory.RegisterDefinition(MakeAmmoDefinition(AmmoTypeId));
            if (StartingReserveAmmo > 0)
            {
                provider.Inventory.AddItem(new ItemInstance(AmmoTypeId, StartingReserveAmmo));
            }
        }

        var instance = new WeaponInstance(WeaponId + "_item", WeaponId) { CurrentAmmo = MagazineSize };

        // Auto-equip only when unarmed; otherwise stash the weapon in the pack (no swap UI yet).
        if (provider?.EquippedWeapon == null)
        {
            holder.EquipWeapon(instance, definition);
            GD.Print($"[Pickup] Equipped {WeaponName} (+{StartingReserveAmmo} {AmmoTypeId}).");
        }
        else if (provider != null)
        {
            provider.Inventory.RegisterDefinition(new ItemResource
            {
                Id = instance.DefinitionId,
                DisplayName = WeaponName,
                MaxStack = 1,
                Weight = 3.0f,
            });
            provider.Inventory.AddItem(instance);
            GD.Print($"[Pickup] Already armed — stored {WeaponName} in the pack.");
        }

        QueueFree();
    }

    internal static ItemResource MakeAmmoDefinition(string ammoId) => new()
    {
        Id = ammoId,
        DisplayName = ammoId,
        MaxStack = 999,
        Weight = 0.01f,
    };
}
