using System;
using System.Collections.Generic;
using Meridian.Combat;
using Meridian.Data;
using Meridian.Items;

namespace Meridian.Core.Registry;

/// <summary>
/// Plain-C# content database populated from index resources at boot. All population logic lives here so it
/// is headless-testable; the Godot <c>ContentDatabaseNode</c> only forwards exported indexes (ADR-0003).
/// </summary>
public sealed class ContentDatabase : IContentDatabase
{
    private readonly Registry<IItemDefinition> _items = new();
    private readonly Registry<IWeaponDefinition> _weapons = new();
    private readonly Registry<ILootTableDefinition> _lootTables = new();
    private readonly Registry<IRegionDefinition> _regions = new();
    private readonly Registry<IWeatherProfile> _weatherProfiles = new();
    private readonly Registry<IMovementProfile> _movementProfiles = new();
    private readonly Registry<IVehicleHandlingProfile> _handlingProfiles = new();
    private readonly List<string> _diagnostics = new();

    /// <inheritdoc/>
    public IRegistry<IItemDefinition> Items => _items;

    /// <inheritdoc/>
    public IRegistry<IWeaponDefinition> Weapons => _weapons;

    /// <inheritdoc/>
    public IRegistry<ILootTableDefinition> LootTables => _lootTables;

    /// <inheritdoc/>
    public IRegistry<IRegionDefinition> Regions => _regions;

    /// <inheritdoc/>
    public IRegistry<IWeatherProfile> WeatherProfiles => _weatherProfiles;

    /// <inheritdoc/>
    public IRegistry<IMovementProfile> MovementProfiles => _movementProfiles;

    /// <inheritdoc/>
    public IRegistry<IVehicleHandlingProfile> HandlingProfiles => _handlingProfiles;

    /// <inheritdoc/>
    public IReadOnlyList<string> Diagnostics => _diagnostics;

    /// <summary>Registers item definitions from the item index; a null feed means an empty category.</summary>
    public void LoadItems(IEnumerable<IItemDefinition?>? definitions)
        => LoadCategory("Items", _items, definitions, static d => d.Id);

    /// <summary>Registers weapon definitions from the weapon index.</summary>
    public void LoadWeapons(IEnumerable<IWeaponDefinition?>? definitions)
        => LoadCategory("Weapons", _weapons, definitions, static d => d.Id);

    /// <summary>Registers loot tables from the loot-table index.</summary>
    public void LoadLootTables(IEnumerable<ILootTableDefinition?>? definitions)
        => LoadCategory("LootTables", _lootTables, definitions, static d => d.Id);

    /// <summary>Registers region definitions from the region index.</summary>
    public void LoadRegions(IEnumerable<IRegionDefinition?>? definitions)
        => LoadCategory("Regions", _regions, definitions, static d => d.Id);

    /// <summary>Registers weather profiles from the weather index.</summary>
    public void LoadWeatherProfiles(IEnumerable<IWeatherProfile?>? definitions)
        => LoadCategory("WeatherProfiles", _weatherProfiles, definitions, static d => d.Id);

    /// <summary>Registers movement profiles from the movement-profile index.</summary>
    public void LoadMovementProfiles(IEnumerable<IMovementProfile?>? definitions)
        => LoadCategory("MovementProfiles", _movementProfiles, definitions, static d => d.Id);

    /// <summary>Registers vehicle handling profiles from the handling-profile index.</summary>
    public void LoadHandlingProfiles(IEnumerable<IVehicleHandlingProfile?>? definitions)
        => LoadCategory("HandlingProfiles", _handlingProfiles, definitions, static d => d.Id);

    // Feeds one category's registry, tagging each registry diagnostic with the category so the validator can
    // report it with context. Null entries (an unassigned index slot, illegal per §19.10) are reported and
    // skipped so one bad row can't abort the load.
    private void LoadCategory<T>(string category, Registry<T> registry, IEnumerable<T?>? definitions, Func<T, string> idSelector)
        where T : class
    {
        if (definitions is null)
        {
            return; // A null index means an empty category, which is legal (§19.1).
        }

        foreach (var definition in definitions)
        {
            if (definition is null)
            {
                _diagnostics.Add($"[{category}] Skipped a null entry in the index.");
                continue;
            }

            int before = registry.Diagnostics.Count;
            registry.Register(idSelector(definition), definition);
            for (int i = before; i < registry.Diagnostics.Count; i++)
            {
                _diagnostics.Add($"[{category}] {registry.Diagnostics[i]}");
            }
        }
    }
}
