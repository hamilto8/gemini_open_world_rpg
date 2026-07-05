using System;

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
