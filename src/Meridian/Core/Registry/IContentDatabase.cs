using System.Collections.Generic;
using Meridian.Combat;
using Meridian.Data;
using Meridian.Items;

namespace Meridian.Core.Registry;

/// <summary>
/// Aggregate of every content registry, one per fixed category (§3.6 item 4). The category set is fixed in
/// code because a new category is code anyway; adding CONTENT requires zero code — only new files plus one
/// index entry (§1.5.7, §19.1). Registered in the Services locator by <c>ContentDatabaseNode</c>.
/// </summary>
public interface IContentDatabase
{
    /// <summary>Item definitions (§7.1).</summary>
    IRegistry<IItemDefinition> Items { get; }

    /// <summary>Weapon definitions (§6.2).</summary>
    IRegistry<IWeaponDefinition> Weapons { get; }

    /// <summary>Loot tables (§7.4).</summary>
    IRegistry<ILootTableDefinition> LootTables { get; }

    /// <summary>Region definitions (§4.2).</summary>
    IRegistry<IRegionDefinition> Regions { get; }

    /// <summary>Weather profiles (§12).</summary>
    IRegistry<IWeatherProfile> WeatherProfiles { get; }

    /// <summary>Movement profiles (§5.2).</summary>
    IRegistry<IMovementProfile> MovementProfiles { get; }

    /// <summary>Vehicle handling profiles (§11.1).</summary>
    IRegistry<IVehicleHandlingProfile> HandlingProfiles { get; }

    /// <summary>Load-time problems collected across all categories (duplicate/empty ids, null entries).</summary>
    IReadOnlyList<string> Diagnostics { get; }
}
