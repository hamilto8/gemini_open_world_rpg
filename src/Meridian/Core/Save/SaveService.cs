using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Meridian.Core.Save;

/// <summary>
/// Core implementation of the save game service.
/// Decoupled from Godot APIs to ensure full headless testability.
/// </summary>
public class SaveService : ISaveService
{
    private readonly List<ISaveParticipant> _participants = new();
    private readonly string _baseDir;
    private readonly string _gameVersion;
    private readonly int _saveVersion;
    private readonly Action<string>? _logger;

    public SaveService(string baseDir, string gameVersion = "1.0.0", int saveVersion = 1, Action<string>? logger = null)
    {
        _baseDir = baseDir;
        _gameVersion = gameVersion;
        _saveVersion = saveVersion;
        _logger = logger;

        if (!Directory.Exists(_baseDir))
        {
            Directory.CreateDirectory(_baseDir);
        }
    }

    public void RegisterParticipant(ISaveParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        if (!_participants.Any(p => p.ParticipantId == participant.ParticipantId))
        {
            _participants.Add(participant);
        }
    }

    public void UnregisterParticipant(ISaveParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        _participants.RemoveAll(p => p.ParticipantId == participant.ParticipantId);
    }

    public bool SaveExists(string slotName)
    {
        string filePath = GetSavePath(slotName);
        return File.Exists(filePath);
    }

    public void DeleteSave(string slotName)
    {
        string filePath = GetSavePath(slotName);
        string bakPath = filePath + ".bak";

        if (File.Exists(filePath)) File.Delete(filePath);
        if (File.Exists(bakPath)) File.Delete(bakPath);
    }

    public void SaveGame(string slotName, string locationName = "Unknown Location")
    {
        // 1. Capture states from all participants synchronously on the main thread
        var states = new Dictionary<string, string>();

        // We capture states and convert each to JSON string using System.Text.Json
        foreach (var participant in _participants)
        {
            var stateObj = participant.CaptureState();
            string json = SerializeState(participant, stateObj);
            states[participant.ParticipantId] = json;
        }

        // 2. Build game save DTO
        var header = new SaveHeaderDto(
            SaveVersion: _saveVersion,
            GameVersion: _gameVersion,
            Timestamp: DateTime.UtcNow,
            PlaytimeSeconds: 0, // Placeholder for Phase 0
            LocationName: locationName,
            ThumbnailPath: ""
        );

        var saveData = new GameSaveData(header, states);

        // 3. Serialize root save data
        string saveDataJson = JsonSerializer.Serialize(saveData, SaveJsonContext.Default.GameSaveData);

        // 4. Perform atomic write
        string finalPath = GetSavePath(slotName);
        string tempPath = finalPath + ".tmp";
        string bakPath = finalPath + ".bak";

        try
        {
            // Write to temporary file
            File.WriteAllText(tempPath, saveDataJson);

            // Rotate existing file to backup
            if (File.Exists(finalPath))
            {
                if (File.Exists(bakPath))
                {
                    File.Delete(bakPath);
                }
                File.Move(finalPath, bakPath);
            }

            // Atomically rename temp file to final file
            File.Move(tempPath, finalPath);
        }
        catch (Exception ex)
        {
            // Cleanup temp file in case of failure
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            throw new InvalidOperationException($"Failed to write save game atomically: {ex.Message}", ex);
        }
    }

    public bool LoadGame(string slotName)
    {
        string primaryPath = GetSavePath(slotName);
        string bakPath = primaryPath + ".bak";

        // Try the primary file first; on missing OR corrupt primary, fall back to the backup (H5).
        if (TryLoadFrom(primaryPath, out bool primaryExisted))
        {
            return true;
        }
        if (primaryExisted)
        {
            _logger?.Invoke($"[SaveService] Primary save '{slotName}' unreadable; attempting backup.");
        }
        return TryLoadFrom(bakPath, out _);
    }

    private bool TryLoadFrom(string filePath, out bool existed)
    {
        existed = File.Exists(filePath);
        if (!existed)
        {
            return false;
        }

        GameSaveData? saveData;
        try
        {
            string jsonContent = File.ReadAllText(filePath);
            saveData = JsonSerializer.Deserialize(jsonContent, SaveJsonContext.Default.GameSaveData);
        }
        catch (Exception ex)
        {
            _logger?.Invoke($"[SaveService] Failed to parse save '{filePath}': {ex.Message}");
            return false;
        }

        if (saveData == null)
        {
            _logger?.Invoke($"[SaveService] Save '{filePath}' deserialized to null.");
            return false;
        }

        RestoreParticipants(saveData);
        return true;
    }

    private void RestoreParticipants(GameSaveData saveData)
    {
        var sortedParticipants = _participants
            .OrderBy(p => p.RestoreOrder)
            .ToList();

        var matchedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var participant in sortedParticipants)
        {
            if (!saveData.ParticipantStatesJson.TryGetValue(participant.ParticipantId, out var stateJson))
            {
                continue;
            }

            matchedIds.Add(participant.ParticipantId);

            // Restore per participant so one bad participant is logged, not silently swallowed,
            // and does not abort the whole restore mid-way (H5).
            try
            {
                object? stateDto = DeserializeState(participant, stateJson);
                if (stateDto != null)
                {
                    participant.RestoreState(stateDto);
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"[SaveService] Participant '{participant.ParticipantId}' failed to restore: {ex.Message}");
            }
        }

        // Surface (don't swallow) saved state that no registered participant claims (H4).
        foreach (var savedId in saveData.ParticipantStatesJson.Keys)
        {
            if (!matchedIds.Contains(savedId))
            {
                _logger?.Invoke($"[SaveService] Save contains state for unregistered participant '{savedId}'; skipped.");
            }
        }
    }

    private string GetSavePath(string slotName)
    {
        return Path.Combine(_baseDir, $"{slotName}.json");
    }

    private static string SerializeState(ISaveParticipant participant, object obj)
    {
        // Serialize by the participant's declared DTO type through the source-generated context
        // (no id-substring guessing, no reflection fallback). Throws loudly if the type is unregistered.
        return JsonSerializer.Serialize(obj, participant.StateType, SaveJsonContext.Default);
    }

    private static object? DeserializeState(ISaveParticipant participant, string json)
    {
        return JsonSerializer.Deserialize(json, participant.StateType, SaveJsonContext.Default);
    }
}
