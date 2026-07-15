using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meridian.Core.Save;

/// <summary>
/// Interface implemented by any subsystem that participates in game saving and loading.
/// </summary>
public interface ISaveParticipant
{
    /// <summary>
    /// Unique identifier for the participant (e.g. "WorldClock", "PlayerState", "Inventory").
    /// </summary>
    string ParticipantId { get; }

    /// <summary>
    /// Order in which the participant should be restored.
    /// Lower numbers restore first (e.g. WorldFlags=10, TimeWeather=20, Domain=50, Possession=100).
    /// </summary>
    int RestoreOrder { get; }

    /// <summary>
    /// The concrete DTO <see cref="Type"/> that <see cref="CaptureState"/> returns and
    /// <see cref="RestoreState"/> accepts. Declared explicitly so the save service can
    /// (de)serialize by type rather than guessing from the participant id.
    /// </summary>
    Type StateType { get; }

    /// <summary>
    /// Version of this participant's DTO. This is independent from the root save-container version,
    /// allowing one gameplay module to evolve without forcing unrelated modules to migrate.
    /// </summary>
    int StateVersion => 1;

    /// <summary>
    /// Captures the current mutable runtime state into a serializable DTO (no Godot Node/Resource references).
    /// Called synchronously on the main thread during save initiation.
    /// </summary>
    object CaptureState();

    /// <summary>
    /// Restores runtime state from the provided DTO.
    /// </summary>
    void RestoreState(object stateDto);
}

/// <summary>
/// Optional participant contract for migrating older module payloads before typed deserialization.
/// Implementations must accept every version from their oldest supported save through
/// <see cref="ISaveParticipant.StateVersion"/> and return JSON in the current schema.
/// </summary>
public interface ISaveStateMigrator
{
    string MigrateStateJson(int sourceVersion, string stateJson);
}

/// <summary>Policy for participant payloads whose owning feature is unavailable in this build.</summary>
public enum UnknownSaveContentPolicy
{
    /// <summary>Keep the opaque JSON and write it back unchanged on the next save.</summary>
    PreserveAndWarn,

    /// <summary>Reject the load rather than risk silently dropping required state.</summary>
    RejectLoad,
}

/// <summary>Canonical restore bands. Features may choose values within a band for finer ordering.</summary>
public static class SaveRestoreOrder
{
    public const int GlobalFlags = 10;
    public const int Environment = 20;
    public const int RegionWarmup = 30;
    public const int WorldObjects = 40;
    public const int Narrative = 50;
    public const int Progression = 60;
    public const int Inventory = 70;
    public const int Equipment = 80;
    public const int PlayerTransform = 90;
    public const int Possession = 100;
    public const int Settings = 200;
}

/// <summary>A root save migration from exactly one container version to the next.</summary>
public interface ISaveMigration
{
    int SourceVersion { get; }
    int TargetVersion { get; }
    GameSaveData Migrate(GameSaveData source);
}

/// <summary>
/// Service coordinating save/load operations across all registered ISaveParticipants.
/// Enforces atomic file writes (temp -> rename over slot -> .bak generation) and ordered restoration.
/// </summary>
public interface ISaveService
{
    void RegisterParticipant(ISaveParticipant participant);
    void UnregisterParticipant(ISaveParticipant participant);

    /// <summary>
    /// Captures state from all registered participants and writes atomically to the designated slot.
    /// </summary>
    void SaveGame(string slotName, string locationName = "Unknown Location");

    /// <summary>
    /// Captures participants on the calling thread, then serializes, flushes, and atomically replaces
    /// the slot in the background. Godot callers should prefer this API to avoid blocking a frame.
    /// </summary>
    Task SaveGameAsync(
        string slotName,
        string locationName = "Unknown Location",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a save file from the designated slot and restores state across all registered participants in RestoreOrder.
    /// </summary>
    bool LoadGame(string slotName);

    /// <summary>
    /// Checks if a save file exists for the given slot.
    /// </summary>
    bool SaveExists(string slotName);

    /// <summary>
    /// Deletes a save slot and its backup if they exist.
    /// </summary>
    void DeleteSave(string slotName);
}
