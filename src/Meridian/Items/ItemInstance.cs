using System;
using System.Collections.Generic;

namespace Meridian.Items;

/// <summary>
/// Runtime mutable instance representing an item in an inventory.
/// References its static resource definition by ID to ensure save forward-compatibility.
/// Enforces Section 7.2 and 3.7 requirements.
/// </summary>
public class ItemInstance
{
    public string DefinitionId { get; }
    public int StackCount { get; set; }
    public Dictionary<string, object> Payload { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ItemInstance(string definitionId, int stackCount = 1)
    {
        ArgumentException.ThrowIfNullOrEmpty(definitionId);
        DefinitionId = definitionId;
        StackCount = stackCount;
    }
}

/// <summary>
/// Mutable runtime instance representing a weapon, tracking magazine ammo and upgrade levels.
/// </summary>
public class WeaponInstance : ItemInstance
{
    public string WeaponDefinitionId { get; }
    public int UpgradeLevel { get; set; } = 0;
    public int CurrentAmmo { get; set; } = 0;
    public List<string> InstalledModIds { get; } = new();

    public WeaponInstance(string itemDefinitionId, string weaponDefinitionId, int stackCount = 1)
        : base(itemDefinitionId, stackCount)
    {
        ArgumentException.ThrowIfNullOrEmpty(weaponDefinitionId);
        WeaponDefinitionId = weaponDefinitionId;
    }
}
