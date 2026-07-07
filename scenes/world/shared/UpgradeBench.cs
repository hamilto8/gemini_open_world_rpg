using Godot;
using Meridian.Core;
using Meridian.Items;

namespace Meridian.Scenes.World;

/// <summary>
/// Workbench interactable that performs atomic upgrades on the player's equipped weapon.
/// Has no private economy logic — it drives the shared <see cref="InventoryTransaction"/> so future
/// benches (garage, gear station) reuse the flow. Enforces Section 6.4 and 7.5 requirements.
/// </summary>
public partial class UpgradeBench : StaticBody3D, IInteractable
{
    public string ObjectName => "Crafting Bench";
    public string ActionPrompt => "Upgrade Weapon";

    [Export] public string RequiredMaterialId { get; set; } = "metal_scrap";
    [Export] public int BaseMaterialCost { get; set; } = 3;

    public bool CanInteract(Node3D interactor)
    {
        // Only interactable when the player actually has a weapon equipped to upgrade.
        return Services.TryGet<IInventoryProvider>(out var provider)
            && provider?.EquippedWeapon != null;
    }

    public void Interact(Node3D interactor)
    {
        if (!Services.TryGet<IInventoryProvider>(out var provider) || provider == null)
        {
            GD.PrintErr("[UpgradeBench] No inventory provider available.");
            return;
        }

        bool success = TryUpgrade(provider);

        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            // Report the real outcome, not an unconditional success (M16).
            eventBus.Publish(new UpgradeAttemptedEvent(ObjectName, success));
        }
    }

    private bool TryUpgrade(IInventoryProvider provider)
    {
        var weapon = provider.EquippedWeapon;
        if (weapon == null)
        {
            return false;
        }

        // Atomic material deduction (Section 7.5): validate-all then apply-all, or nothing.
        var transaction = new InventoryTransaction();
        transaction.DeductItem(provider.Inventory, RequiredMaterialId, BaseMaterialCost);

        if (!transaction.Execute())
        {
            GD.Print($"[UpgradeBench] Upgrade aborted: not enough '{RequiredMaterialId}'.");
            return false;
        }

        weapon.UpgradeLevel++;
        GD.Print($"[UpgradeBench] Upgraded weapon to level {weapon.UpgradeLevel}.");
        return true;
    }
}

/// <summary>
/// Event published on EventBus when an upgrade is attempted at a bench.
/// </summary>
public record struct UpgradeAttemptedEvent(string BenchName, bool Success);
