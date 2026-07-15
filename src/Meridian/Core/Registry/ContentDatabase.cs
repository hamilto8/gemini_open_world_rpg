using System;
using System.Collections.Generic;
using Meridian.Combat;
using Meridian.Data;
using Meridian.Items;
using Meridian.Quests;
using Meridian.Dialogue;
using Meridian.NPC;
using Meridian.Environment;
using Meridian.Factions;
using Meridian.World;
using Meridian.Audio;

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
    private readonly Registry<IQuestDefinition> _quests = new();
    private readonly Registry<IDialogueDefinition> _dialogues = new();
    private readonly Registry<INpcDefinition> _npcs = new();
    private readonly Registry<IScheduledEventDefinition> _scheduledEvents = new();
    private readonly Registry<IFactionDefinition> _factions = new();
    private readonly Registry<IFastTravelPointDefinition> _fastTravelPoints = new();
    private readonly Registry<IProgressionProfile> _progressionProfiles = new();
    private readonly Registry<ISoundCueDefinition> _soundCues = new();
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
    public IRegistry<IQuestDefinition> Quests => _quests;
    public IRegistry<IDialogueDefinition> Dialogues => _dialogues;
    public IRegistry<INpcDefinition> Npcs => _npcs;
    public IRegistry<IScheduledEventDefinition> ScheduledEvents => _scheduledEvents;
    public IRegistry<IFactionDefinition> Factions => _factions;
    public IRegistry<IFastTravelPointDefinition> FastTravelPoints => _fastTravelPoints;
    public IRegistry<IProgressionProfile> ProgressionProfiles => _progressionProfiles;
    public IRegistry<ISoundCueDefinition> SoundCues => _soundCues;

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

    public void LoadQuests(IEnumerable<IQuestDefinition?>? definitions)
        => LoadCategory("Quests", _quests, definitions, static d => d.QuestId);

    public void LoadDialogues(IEnumerable<IDialogueDefinition?>? definitions)
        => LoadCategory("Dialogues", _dialogues, definitions, static d => d.Id);

    public void LoadNpcs(IEnumerable<INpcDefinition?>? definitions)
        => LoadCategory("Npcs", _npcs, definitions, static d => d.Id);

    public void LoadScheduledEvents(IEnumerable<IScheduledEventDefinition?>? definitions)
        => LoadCategory("ScheduledEvents", _scheduledEvents, definitions, static d => d.Id);

    public void LoadFactions(IEnumerable<IFactionDefinition?>? definitions)
        => LoadCategory("Factions", _factions, definitions, static d => d.Id);

    public void LoadFastTravelPoints(IEnumerable<IFastTravelPointDefinition?>? definitions)
        => LoadCategory("FastTravelPoints", _fastTravelPoints, definitions, static d => d.Id);

    public void LoadProgressionProfiles(IEnumerable<IProgressionProfile?>? definitions)
        => LoadCategory("ProgressionProfiles", _progressionProfiles, definitions, static d => d.Id);

    public void LoadSoundCues(IEnumerable<ISoundCueDefinition?>? definitions)
        => LoadCategory("SoundCues", _soundCues, definitions, static d => d.Id);

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
