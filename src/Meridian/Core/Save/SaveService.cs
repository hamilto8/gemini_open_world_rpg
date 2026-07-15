using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Meridian.Core.Save;

/// <summary>
/// Headless save orchestrator. Runtime objects are captured on the calling thread; JSON serialization
/// and durable file I/O run on a worker. Loads migrate the root container and each participant before
/// restoring modules in deterministic order.
/// </summary>
public sealed class SaveService : ISaveService
{
    public const int CurrentSaveVersion = 2;

    private readonly List<ISaveParticipant> _participants = new();
    private readonly object _participantLock = new();
    private readonly object _unknownStateLock = new();
    private readonly SemaphoreSlim _fileGate = new(1, 1);
    private readonly string _baseDir;
    private readonly string _gameVersion;
    private readonly int _saveVersion;
    private readonly Action<string>? _logger;
    private readonly SaveMigrationPipeline _migrationPipeline;
    private readonly UnknownSaveContentPolicy _unknownContentPolicy;

    // Unknown modules from the most recently loaded save are retained verbatim. This makes temporary
    // feature removal, DLC absence, and mod churn non-destructive across load -> save cycles.
    private Dictionary<string, string> _preservedUnknownStates = new(StringComparer.Ordinal);
    private Dictionary<string, int> _preservedUnknownVersions = new(StringComparer.Ordinal);

    public SaveService(
        string baseDir,
        string gameVersion = "1.0.0",
        int saveVersion = CurrentSaveVersion,
        Action<string>? logger = null,
        SaveMigrationPipeline? migrationPipeline = null,
        UnknownSaveContentPolicy unknownContentPolicy = UnknownSaveContentPolicy.PreserveAndWarn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);
        if (saveVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(saveVersion));
        }

        _baseDir = baseDir;
        _gameVersion = gameVersion;
        _saveVersion = saveVersion;
        _logger = logger;
        _migrationPipeline = migrationPipeline ?? new SaveMigrationPipeline();
        _unknownContentPolicy = unknownContentPolicy;
        Directory.CreateDirectory(_baseDir);
    }

    public void RegisterParticipant(ISaveParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        if (string.IsNullOrWhiteSpace(participant.ParticipantId))
        {
            throw new ArgumentException("Save participant ids cannot be empty.", nameof(participant));
        }
        if (participant.StateVersion < 1)
        {
            throw new ArgumentException("Save participant versions must be positive.", nameof(participant));
        }

        lock (_participantLock)
        {
            if (_participants.Any(p => p.ParticipantId.Equals(participant.ParticipantId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Save participant '{participant.ParticipantId}' is already registered.");
            }
            _participants.Add(participant);
        }
    }

    public void UnregisterParticipant(ISaveParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        lock (_participantLock)
        {
            _participants.RemoveAll(p => ReferenceEquals(p, participant));
        }
    }

    public bool SaveExists(string slotName) => File.Exists(GetSavePath(slotName));

    public void DeleteSave(string slotName)
    {
        string filePath = GetSavePath(slotName);
        DeleteIfExists(filePath);
        DeleteIfExists(filePath + ".bak");
        DeleteIfExists(filePath + ".tmp");
    }

    public void SaveGame(string slotName, string locationName = "Unknown Location")
    {
        SaveGameAsync(slotName, locationName).GetAwaiter().GetResult();
    }

    public async Task SaveGameAsync(
        string slotName,
        string locationName = "Unknown Location",
        CancellationToken cancellationToken = default)
    {
        string finalPath = GetSavePath(slotName); // Validate before capturing mutable state.
        ISaveParticipant[] participants = ParticipantSnapshot();
        Dictionary<string, string> unknownStates;
        Dictionary<string, int> unknownVersions;
        lock (_unknownStateLock)
        {
            unknownStates = new Dictionary<string, string>(_preservedUnknownStates, StringComparer.Ordinal);
            unknownVersions = new Dictionary<string, int>(_preservedUnknownVersions, StringComparer.Ordinal);
        }
        var captures = new List<CapturedParticipantState>(participants.Length);

        // Capture stays on the caller (Godot main) thread. A DTO must be a detached snapshot; after
        // this loop no gameplay object is touched by worker code.
        foreach (var participant in participants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            object state = participant.CaptureState();
            captures.Add(new CapturedParticipantState(participant, state));
        }

        await _fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(async () =>
            {
                var states = new Dictionary<string, string>(unknownStates, StringComparer.Ordinal);
                var versions = new Dictionary<string, int>(unknownVersions, StringComparer.Ordinal);
                foreach (var capture in captures)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    states[capture.Participant.ParticipantId] = SerializeState(capture.Participant, capture.State);
                    versions[capture.Participant.ParticipantId] = capture.Participant.StateVersion;
                }

                var saveData = new GameSaveData(
                    new SaveHeaderDto(
                        _saveVersion,
                        _gameVersion,
                        DateTime.UtcNow,
                        0,
                        locationName,
                        string.Empty),
                    states,
                    versions);
                await WriteDurablyAsync(finalPath, saveData, cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fileGate.Release();
        }
    }

    public bool LoadGame(string slotName)
    {
        string primaryPath = GetSavePath(slotName);
        string backupPath = primaryPath + ".bak";

        if (TryReadAndMigrate(primaryPath, out GameSaveData? primary, out bool primaryExisted))
        {
            return RestoreParticipants(primary!);
        }
        if (primaryExisted)
        {
            _logger?.Invoke($"[SaveService] Primary save '{slotName}' unreadable; attempting backup.");
        }

        return TryReadAndMigrate(backupPath, out GameSaveData? backup, out _)
            && RestoreParticipants(backup!);
    }

    private bool TryReadAndMigrate(string filePath, out GameSaveData? saveData, out bool existed)
    {
        existed = File.Exists(filePath);
        saveData = null;
        if (!existed)
        {
            return false;
        }

        try
        {
            string jsonContent = File.ReadAllText(filePath);
            GameSaveData? parsed = JsonSerializer.Deserialize(jsonContent, SaveJsonContext.Default.GameSaveData);
            if (parsed == null)
            {
                _logger?.Invoke($"[SaveService] Save '{filePath}' deserialized to null.");
                return false;
            }
            if (parsed.Header == null || parsed.ParticipantStatesJson == null)
            {
                _logger?.Invoke($"[SaveService] Save '{filePath}' is missing required container fields.");
                return false;
            }

            saveData = _migrationPipeline.MigrateTo(parsed, _saveVersion);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException or InvalidOperationException)
        {
            _logger?.Invoke($"[SaveService] Failed to read save '{filePath}': {ex.Message}");
            return false;
        }
    }

    private bool RestoreParticipants(GameSaveData saveData)
    {
        ISaveParticipant[] participants = ParticipantSnapshot()
            .OrderBy(p => p.RestoreOrder)
            .ThenBy(p => p.ParticipantId, StringComparer.Ordinal)
            .ToArray();
        var matchedIds = new HashSet<string>(StringComparer.Ordinal);
        var unknownStates = new Dictionary<string, string>(StringComparer.Ordinal);
        var unknownVersions = new Dictionary<string, int>(StringComparer.Ordinal);
        bool succeeded = true;

        if (_unknownContentPolicy == UnknownSaveContentPolicy.RejectLoad)
        {
            var registeredIds = new HashSet<string>(participants.Select(p => p.ParticipantId), StringComparer.Ordinal);
            string? unavailableId = saveData.ParticipantStatesJson.Keys.FirstOrDefault(id => !registeredIds.Contains(id));
            if (unavailableId != null)
            {
                _logger?.Invoke($"[SaveService] Required participant '{unavailableId}' is unavailable; load rejected.");
                return false;
            }
        }

        foreach (var participant in participants)
        {
            if (!saveData.ParticipantStatesJson.TryGetValue(participant.ParticipantId, out string? stateJson))
            {
                continue;
            }

            matchedIds.Add(participant.ParticipantId);
            int savedVersion = GetParticipantVersion(saveData, participant.ParticipantId);
            try
            {
                string currentJson = MigrateParticipantState(participant, savedVersion, stateJson);
                object? stateDto = DeserializeState(participant, currentJson);
                if (stateDto == null)
                {
                    throw new JsonException("Participant state deserialized to null.");
                }
                participant.RestoreState(stateDto);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
            {
                succeeded = false;
                _logger?.Invoke($"[SaveService] Participant '{participant.ParticipantId}' failed to restore: {ex.Message}");
            }
        }

        foreach (var (savedId, json) in saveData.ParticipantStatesJson)
        {
            if (matchedIds.Contains(savedId))
            {
                continue;
            }
            unknownStates[savedId] = json;
            unknownVersions[savedId] = GetParticipantVersion(saveData, savedId);
            _logger?.Invoke($"[SaveService] Preserving state for unavailable participant '{savedId}'.");
        }

        if (succeeded)
        {
            lock (_unknownStateLock)
            {
                _preservedUnknownStates = unknownStates;
                _preservedUnknownVersions = unknownVersions;
            }
        }
        return succeeded;
    }

    private static int GetParticipantVersion(GameSaveData saveData, string participantId)
    {
        return saveData.ParticipantStateVersions != null
            && saveData.ParticipantStateVersions.TryGetValue(participantId, out int version)
            ? version
            : 1;
    }

    private static string MigrateParticipantState(ISaveParticipant participant, int savedVersion, string json)
    {
        if (savedVersion == participant.StateVersion)
        {
            return json;
        }
        if (savedVersion > participant.StateVersion)
        {
            throw new NotSupportedException(
                $"State version {savedVersion} is newer than supported version {participant.StateVersion}.");
        }
        if (participant is not ISaveStateMigrator migrator)
        {
            throw new NotSupportedException(
                $"No state migration from version {savedVersion} to {participant.StateVersion} is registered.");
        }
        return migrator.MigrateStateJson(savedVersion, json);
    }

    private async Task WriteDurablyAsync(
        string finalPath,
        GameSaveData saveData,
        CancellationToken cancellationToken)
    {
        string tempPath = finalPath + ".tmp";
        string backupPath = finalPath + ".bak";
        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    saveData,
                    SaveJsonContext.Default.GameSaveData,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(finalPath))
            {
                ReplaceWithBackup(tempPath, finalPath, backupPath);
            }
            else
            {
                File.Move(tempPath, finalPath);
            }
        }
        catch (Exception ex)
        {
            DeleteIfExists(tempPath);
            if (ex is OperationCanceledException)
            {
                throw;
            }
            throw new InvalidOperationException($"Failed to write save game atomically: {ex.Message}", ex);
        }
    }

    private static void ReplaceWithBackup(string tempPath, string finalPath, string backupPath)
    {
        try
        {
            // Same-volume File.Replace is atomic and creates the backup as part of the operation.
            File.Replace(tempPath, finalPath, backupPath, ignoreMetadataErrors: true);
        }
        catch (PlatformNotSupportedException)
        {
            PortableReplace(tempPath, finalPath, backupPath);
        }
        catch (IOException)
        {
            // Some Unix file systems reject File.Replace despite supporting atomic rename-overwrite.
            PortableReplace(tempPath, finalPath, backupPath);
        }
    }

    private static void PortableReplace(string tempPath, string finalPath, string backupPath)
    {
        File.Copy(finalPath, backupPath, overwrite: true);
        using (var backup = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            backup.Flush(flushToDisk: true);
        }
        File.Move(tempPath, finalPath, overwrite: true);
    }

    private ISaveParticipant[] ParticipantSnapshot()
    {
        lock (_participantLock)
        {
            return _participants.ToArray();
        }
    }

    private string GetSavePath(string slotName)
    {
        ValidateSlotName(slotName);
        return Path.Combine(_baseDir, $"{slotName}.json");
    }

    private static void ValidateSlotName(string slotName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotName);
        if (slotName is "." or ".." || slotName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || slotName.Contains(Path.DirectorySeparatorChar)
            || slotName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Save slot names must be a single valid file-name component.", nameof(slotName));
        }
    }

    private static string SerializeState(ISaveParticipant participant, object state)
    {
        if (!participant.StateType.IsInstanceOfType(state))
        {
            throw new InvalidOperationException(
                $"Participant '{participant.ParticipantId}' captured {state.GetType().Name}; expected {participant.StateType.Name}.");
        }
        return JsonSerializer.Serialize(state, participant.StateType, SaveJsonContext.Default);
    }

    private static object? DeserializeState(ISaveParticipant participant, string json)
    {
        return JsonSerializer.Deserialize(json, participant.StateType, SaveJsonContext.Default);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed record CapturedParticipantState(ISaveParticipant Participant, object State);
}
