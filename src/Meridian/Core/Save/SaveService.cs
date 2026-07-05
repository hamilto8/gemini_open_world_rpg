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

    public SaveService(string baseDir, string gameVersion = "1.0.0", int saveVersion = 1)
    {
        _baseDir = baseDir;
        _gameVersion = gameVersion;
        _saveVersion = saveVersion;

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
            string json = SerializeState(stateObj);
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
        string filePath = GetSavePath(slotName);
        if (!File.Exists(filePath))
        {
            // Fall back to backup if original is missing/corrupt
            filePath = filePath + ".bak";
            if (!File.Exists(filePath))
            {
                return false;
            }
        }

        try
        {
            string jsonContent = File.ReadAllText(filePath);
            var saveData = JsonSerializer.Deserialize(jsonContent, SaveJsonContext.Default.GameSaveData);
            if (saveData == null)
            {
                return false;
            }

            // 1. Sort participants by RestoreOrder
            var sortedParticipants = _participants
                .OrderBy(p => p.RestoreOrder)
                .ToList();

            // 2. Restore state for each participant
            foreach (var participant in sortedParticipants)
            {
                if (saveData.ParticipantStatesJson.TryGetValue(participant.ParticipantId, out var stateJson))
                {
                    object? stateDto = DeserializeState(participant.ParticipantId, stateJson);
                    if (stateDto != null)
                    {
                        participant.RestoreState(stateDto);
                    }
                }
            }

            return true;
        }
        catch (Exception)
        {
            // Return false on deserialization failure or restore exception
            return false;
        }
    }

    private string GetSavePath(string slotName)
    {
        return Path.Combine(_baseDir, $"{slotName}.json");
    }

    private string SerializeState(object obj)
    {
        // Use type-matching to invoke the correct context serialization
        if (obj is PlayerStateDto player) return JsonSerializer.Serialize(player, SaveJsonContext.Default.PlayerStateDto);
        if (obj is WorldFlagsDto flags) return JsonSerializer.Serialize(flags, SaveJsonContext.Default.WorldFlagsDto);
        if (obj is TimeWeatherDto tw) return JsonSerializer.Serialize(tw, SaveJsonContext.Default.TimeWeatherDto);

        // Fallback for custom objects / generic fallback using basic serialization (not fully trimming-safe but necessary for test extensions)
        return JsonSerializer.Serialize(obj, obj.GetType());
    }

    private object? DeserializeState(string id, string json)
    {
        // Find expected type based on id convention
        if (id.Contains("Player", StringComparison.OrdinalIgnoreCase)) return JsonSerializer.Deserialize(json, SaveJsonContext.Default.PlayerStateDto);
        if (id.Contains("Flag", StringComparison.OrdinalIgnoreCase)) return JsonSerializer.Deserialize(json, SaveJsonContext.Default.WorldFlagsDto);
        if (id.Contains("Time", StringComparison.OrdinalIgnoreCase) || id.Contains("Weather", StringComparison.OrdinalIgnoreCase)) return JsonSerializer.Deserialize(json, SaveJsonContext.Default.TimeWeatherDto);

        return null;
    }
}
