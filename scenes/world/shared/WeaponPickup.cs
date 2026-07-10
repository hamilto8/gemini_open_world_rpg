using Godot;
using Meridian.Combat;
using Meridian.Core;
using Meridian.Core.Registry;
using Meridian.Items;

namespace Meridian.Scenes.World;

/// <summary>
/// World interactable that equips a weapon (and stocks starting reserve ammo) on interact.
/// Resolves its weapon/ammo/stash <see cref="IWeaponDefinition"/>/<see cref="IItemDefinition"/> from the
/// data-driven <see cref="IContentDatabase"/> by id (§19.1) — the exported ids are the only scene-tunable
/// surface; the stats live in authored .tres. A missing database or unknown id is a data error surfaced
/// loudly (the ContentValidator catches it at authoring time), and the pickup stays in the world.
/// </summary>
public partial class WeaponPickup : StaticBody3D, IInteractable
{
    [Export] public string WeaponId { get; set; } = "test_pistol";
    [Export] public string WeaponName { get; set; } = "Test Pistol";
    [Export] public string AmmoTypeId { get; set; } = "ammo_9mm";
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

        // Weapon/item definitions are authored data now; resolve from the registry instead of building them
        // inline. Missing database or unknown id → warn and leave the pickup in the world (data error).
        if (!Services.TryGet<IContentDatabase>(out var content) || content == null)
        {
            GD.PushWarning("[WeaponPickup] No IContentDatabase registered; cannot resolve weapon definitions.");
            return;
        }

        if (!content.Weapons.TryGet(WeaponId, out var definition) || definition == null)
        {
            GD.PushWarning($"[WeaponPickup] Unknown weapon id '{WeaponId}'; not in the Weapons registry.");
            return;
        }

        Services.TryGet<IInventoryProvider>(out var provider);

        var instance = new WeaponInstance(WeaponId + "_item", WeaponId) { CurrentAmmo = definition.MagazineSize };

        // Acquire the weapon first; bundled reserve ammo is only granted once the weapon itself
        // landed. Auto-equip only when unarmed; otherwise stash it in the pack (no swap UI yet).
        if (provider?.EquippedWeapon == null)
        {
            holder.EquipWeapon(instance, definition);
            GD.Print($"[Pickup] Equipped {WeaponName}.");
        }
        else
        {
            if (!content.Items.TryGet(instance.DefinitionId, out var stashDefinition) || stashDefinition == null)
            {
                GD.PushWarning($"[WeaponPickup] Unknown stash item id '{instance.DefinitionId}'; not in the Items registry.");
                return;
            }

            provider.Inventory.RegisterDefinition(stashDefinition);
            if (!provider.Inventory.AddItem(instance))
            {
                // A full pack must not destroy the weapon — leave the pickup in the world.
                PublishNotice($"Pack is full — can't carry the {WeaponName}.");
                return;
            }
            GD.Print($"[Pickup] Already armed — stored {WeaponName} in the pack.");
        }

        // Stock reserve ammo so reloading works. The weapon is already taken at this point, so an
        // over-weight failure here only forfeits the bonus ammo (the pickup node is consumed either way).
        if (provider != null && StartingReserveAmmo > 0)
        {
            if (!content.Items.TryGet(AmmoTypeId, out var ammoDefinition) || ammoDefinition == null)
            {
                GD.PushWarning($"[WeaponPickup] Unknown ammo id '{AmmoTypeId}'; reserve ammo not granted.");
            }
            else
            {
                provider.Inventory.RegisterDefinition(ammoDefinition);
                if (provider.Inventory.AddItem(new ItemInstance(AmmoTypeId, StartingReserveAmmo)))
                {
                    GD.Print($"[Pickup] +{StartingReserveAmmo} {AmmoTypeId}.");
                }
                else
                {
                    PublishNotice($"Pack is full — left the spare {AmmoTypeId} behind.");
                }
            }
        }

        QueueFree();
    }

    /// <summary>Publishes a transient HUD toast (shared by the pickup/container interactables).</summary>
    internal static void PublishNotice(string message)
    {
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new HudNoticeEvent(message));
        }
    }
}
