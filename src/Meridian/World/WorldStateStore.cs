using System;
using System.Collections.Generic;
using Meridian.Core.Save;

namespace Meridian.World;

/// <summary>
/// Stores modifications and dynamic object records per cell to ensure persistence.
/// Keyed by cell unique identifiers. Registers as a SaveParticipant.
/// Enforces Section 4.3 and 16.1 requirements.
/// </summary>
public class WorldStateStore : ISaveParticipant
{
    /// <summary>Snapshot of one currently-instanced cell, supplied by the streamer at capture time.</summary>
    public readonly record struct LiveCellState(
        string CellId,
        Dictionary<string, string> Deltas,
        List<DynamicObjectRecordDto> DynamicObjects);

    private sealed class CellRecord
    {
        public Dictionary<string, string> Deltas = new();
        public List<DynamicObjectRecordDto> DynamicObjects = new();
    }

    private static readonly IReadOnlyList<DynamicObjectRecordDto> EmptyRecords = Array.Empty<DynamicObjectRecordDto>();

    private readonly Dictionary<string, CellRecord> _cells = new(StringComparer.OrdinalIgnoreCase);

    public string ParticipantId => "WorldStateStore";
    public int RestoreOrder => 15; // Restores immediately after world flags but before environments and player
    public Type StateType => typeof(WorldStateDto);

    /// <summary>
    /// Installed by the streamer so <see cref="CaptureState"/> reflects cells that are still loaded:
    /// deltas are otherwise flushed only on cell unload, and a save taken while standing inside a
    /// modified cell would silently lose its changes (Phase 3 exit criterion, doc §4.3).
    /// </summary>
    public Func<IEnumerable<LiveCellState>>? LiveStateProvider { get; set; }

    /// <summary>
    /// Raised after a successful <see cref="RestoreState"/> so the streamer can rebuild
    /// currently-instanced cells from the restored records instead of stale in-scene state.
    /// </summary>
    public event Action? StateRestored;

    public void SaveCellState(string cellId, Dictionary<string, string> deltas)
    {
        ArgumentException.ThrowIfNullOrEmpty(cellId);
        GetOrAddCell(cellId).Deltas = deltas ?? new Dictionary<string, string>();
    }

    public bool TryGetCellState(string cellId, out Dictionary<string, string>? deltas)
    {
        ArgumentException.ThrowIfNullOrEmpty(cellId);
        if (_cells.TryGetValue(cellId, out var record))
        {
            deltas = record.Deltas;
            return true;
        }
        deltas = null;
        return false;
    }

    public void SaveCellDynamicObjects(string cellId, List<DynamicObjectRecordDto> records)
    {
        ArgumentException.ThrowIfNullOrEmpty(cellId);
        GetOrAddCell(cellId).DynamicObjects = records ?? new List<DynamicObjectRecordDto>();
    }

    /// <summary>Dynamic-object respawn records for a cell (empty when none were captured).</summary>
    public IReadOnlyList<DynamicObjectRecordDto> GetCellDynamicObjects(string cellId)
    {
        ArgumentException.ThrowIfNullOrEmpty(cellId);
        return _cells.TryGetValue(cellId, out var record) ? record.DynamicObjects : EmptyRecords;
    }

    public void Clear()
    {
        _cells.Clear();
    }

    public object CaptureState()
    {
        // Fold in the streamer's live cells first so a save taken mid-cell wins over stale unload
        // snapshots; copies keep the DTO independent of later in-place mutation.
        if (LiveStateProvider != null)
        {
            foreach (var live in LiveStateProvider())
            {
                SaveCellState(live.CellId, live.Deltas);
                SaveCellDynamicObjects(live.CellId, live.DynamicObjects);
            }
        }

        var cells = new Dictionary<string, CellStateDto>(_cells.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (cellId, record) in _cells)
        {
            cells[cellId] = new CellStateDto(
                new Dictionary<string, string>(record.Deltas),
                new List<DynamicObjectRecordDto>(record.DynamicObjects));
        }
        return new WorldStateDto(cells);
    }

    public void RestoreState(object stateDto)
    {
        // Wrong-shaped payloads (including pre-WorldStateDto legacy saves, which deserialize with
        // Cells == null) must degrade to "no recorded world state", never crash the load (§16.3).
        if (stateDto is not WorldStateDto dto) return;

        _cells.Clear();
        if (dto.Cells != null)
        {
            foreach (var (cellId, cell) in dto.Cells)
            {
                if (string.IsNullOrEmpty(cellId) || cell == null) continue;
                var record = GetOrAddCell(cellId);
                record.Deltas = cell.ObjectDeltas ?? new Dictionary<string, string>();
                record.DynamicObjects = cell.DynamicObjects ?? new List<DynamicObjectRecordDto>();
            }
        }

        StateRestored?.Invoke();
    }

    private CellRecord GetOrAddCell(string cellId)
    {
        if (!_cells.TryGetValue(cellId, out var record))
        {
            record = new CellRecord();
            _cells[cellId] = record;
        }
        return record;
    }
}
