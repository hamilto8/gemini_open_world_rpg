namespace Meridian.Items;

/// <summary>
/// Exposes the player's inventory and currently equipped weapon to systems that need to transact
/// against them (upgrade/crafting benches, vendors) without reaching into the controller Node
/// directly. Registered by the PlayerController (doc §5.1 — the controller holds the player's
/// identity, including the inventory reference).
/// </summary>
public interface IInventoryProvider
{
    /// <summary>The player's inventory container.</summary>
    InventoryModel Inventory { get; }

    /// <summary>The currently equipped weapon instance, or null if none is equipped.</summary>
    WeaponInstance? EquippedWeapon { get; set; }
}
