using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Meridian.Core.Save;

/// <summary>
/// Header information attached to every save file.
/// </summary>
public record SaveHeaderDto(
    int SaveVersion,
    string GameVersion,
    DateTime Timestamp,
    double PlaytimeSeconds,
    string LocationName,
    string ThumbnailPath
);

/// <summary>
/// Root container serialized to disk for a save game.
/// Holds modular JSON strings for each registered ISaveParticipant, ensuring forward compatibility.
/// </summary>
public sealed record GameSaveData
{
    public SaveHeaderDto Header { get; init; }
    public Dictionary<string, string> ParticipantStatesJson { get; init; }

    /// <summary>
    /// Per-participant DTO versions. Absent in v1 saves; the v1-to-v2 migration initializes entries to 1.
    /// </summary>
    public Dictionary<string, int>? ParticipantStateVersions { get; init; }

    [JsonConstructor]
    public GameSaveData(
        SaveHeaderDto header,
        Dictionary<string, string> participantStatesJson,
        Dictionary<string, int>? participantStateVersions = null)
    {
        Header = header;
        ParticipantStatesJson = participantStatesJson;
        ParticipantStateVersions = participantStateVersions;
    }
}

/// <summary>
/// Standard player state DTO.
/// </summary>
public record PlayerStateDto(
    string CurrentRegionId,
    float PositionX, float PositionY, float PositionZ,
    float RotationY,
    float Health,
    float Stamina,
    string PossessedGuid
);

/// <summary>Serializable item instance. Weapon-only fields are null/default for ordinary items.</summary>
public record ItemInstanceDto(
    string DefinitionId,
    int StackCount,
    Dictionary<string, string> Payload,
    string? WeaponDefinitionId,
    int UpgradeLevel,
    int CurrentAmmo,
    List<string> InstalledModIds
);

/// <summary>Player inventory/equipped-weapon module, separate from transform/vitals state.</summary>
public record InventoryStateDto(
    float MaxWeight,
    List<ItemInstanceDto> Items,
    ItemInstanceDto? EquippedWeapon
);

/// <summary>Character progression state. Perk ids are stable content ids.</summary>
public record ProgressionStateDto(
    int Level,
    int CurrentXp,
    int SkillPoints,
    List<string> UnlockedPerkIds
);

/// <summary>Equipped item instances keyed by stable slot id.</summary>
public record EquipmentStateDto(Dictionary<string, ItemInstanceDto> Slots);

/// <summary>Quick-access bindings keyed by authored bar index.</summary>
public record QuickSlotsStateDto(Dictionary<int, string> ContentIds);

/// <summary>Known faction reputation values keyed by stable faction id.</summary>
public record FactionStateDto(Dictionary<string, int> Reputation);

/// <summary>World discoveries represented by stable discovery ids.</summary>
public record DiscoveriesStateDto(List<string> DiscoveredIds);

/// <summary>Player accessibility and presentation preferences stored as engine-free values.</summary>
public record PlayerSettingsDto(
    bool SubtitlesEnabled,
    float TextScale,
    Dictionary<string, string> KeyBindings
);

/// <summary>Mutable runtime state for a persistent vehicle.</summary>
public record VehicleStateDto(
    string PersistentId,
    string DefinitionId,
    string RegionId,
    float PositionX, float PositionY, float PositionZ,
    float RotationY,
    float Fuel,
    float Health,
    bool IsPlayerPossessed,
    Dictionary<string, string> CustomState
);

/// <summary>All persistent vehicles known to the current world.</summary>
public record VehicleFleetStateDto(List<VehicleStateDto> Vehicles);

/// <summary>
/// Standard world flags DTO for consequence memory.
/// </summary>
public record WorldFlagsDto(Dictionary<string, string> Flags);

/// <summary>
/// Standard time and weather state DTO.
/// </summary>
public record TimeWeatherDto(
    double TotalGameMinutes,
    int DayCounter,
    string CurrentWeatherId,
    float WeatherIntensity,
    float WeatherElapsed,
    int ForecastSeed
);

/// <summary>Deterministic weather state-machine cursor, independent from visual transition state.</summary>
public record WeatherForecastStateDto(
    string CurrentWeatherId,
    int RemainingMinutes,
    uint RandomState
);

/// <summary>
/// Quest progress DTO: quest states plus per-objective progress counters.
/// </summary>
public record QuestSaveDto(
    Dictionary<string, string> QuestStates,
    Dictionary<string, Dictionary<string, int>> ObjectiveProgress
);

/// <summary>
/// A runtime-spawned object (dropped item, parked vehicle) recorded per cell so streaming can
/// recreate it on reload — authored objects only need deltas re-applied, dynamic ones need a scene
/// to respawn from (doc §4.3). Engine-free primitives: vectors are decomposed like PlayerStateDto.
/// </summary>
public record DynamicObjectRecordDto(
    string PersistentId,
    string ScenePath,
    float PosX, float PosY, float PosZ,
    float RotY,
    Dictionary<string, string> State
);

/// <summary>Per-cell world state: authored-object deltas plus runtime-spawned object records.</summary>
public record CellStateDto(
    Dictionary<string, string> ObjectDeltas,
    List<DynamicObjectRecordDto> DynamicObjects
);

/// <summary>
/// WorldStateStore DTO — the §16.1 "DynamicObjects" consequence-memory module, keyed by cell id.
/// Replaces the earlier reuse of <see cref="WorldFlagsDto"/> (which belongs to the flags participant).
/// </summary>
public record WorldStateDto(Dictionary<string, CellStateDto> Cells);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GameSaveData))]
[JsonSerializable(typeof(SaveHeaderDto))]
[JsonSerializable(typeof(PlayerStateDto))]
[JsonSerializable(typeof(ItemInstanceDto))]
[JsonSerializable(typeof(InventoryStateDto))]
[JsonSerializable(typeof(ProgressionStateDto))]
[JsonSerializable(typeof(EquipmentStateDto))]
[JsonSerializable(typeof(QuickSlotsStateDto))]
[JsonSerializable(typeof(FactionStateDto))]
[JsonSerializable(typeof(DiscoveriesStateDto))]
[JsonSerializable(typeof(PlayerSettingsDto))]
[JsonSerializable(typeof(VehicleStateDto))]
[JsonSerializable(typeof(VehicleFleetStateDto))]
[JsonSerializable(typeof(WorldFlagsDto))]
[JsonSerializable(typeof(TimeWeatherDto))]
[JsonSerializable(typeof(WeatherForecastStateDto))]
[JsonSerializable(typeof(QuestSaveDto))]
[JsonSerializable(typeof(WorldStateDto))]
[JsonSerializable(typeof(CellStateDto))]
[JsonSerializable(typeof(DynamicObjectRecordDto))]
[JsonSerializable(typeof(Dictionary<string, CellStateDto>))]
[JsonSerializable(typeof(List<DynamicObjectRecordDto>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<ItemInstanceDto>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, int>>))]
[JsonSerializable(typeof(Dictionary<string, ItemInstanceDto>))]
[JsonSerializable(typeof(Dictionary<int, string>))]
public partial class SaveJsonContext : JsonSerializerContext
{
}
