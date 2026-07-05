using System;
using System.Collections.Generic;
using Meridian.Core;
using Meridian.Core.Save;

namespace Meridian.World;

/// <summary>
/// Stores modifications and dynamic object deltas per cell to ensure persistence.
/// Keyed by cell unique identifiers. Registers as a SaveParticipant.
/// Enforces Section 4.3 and 16.1 requirements.
/// </summary>
public class WorldStateStore : ISaveParticipant
{
    private readonly Dictionary<string, Dictionary<string, string>> _cellStates = new(StringComparer.OrdinalIgnoreCase);

    public string ParticipantId => "WorldStateStore";
    public int RestoreOrder => 15; // Restores immediately after world flags but before environments and player

    public void SaveCellState(string cellId, Dictionary<string, string> deltas)
    {
        ArgumentException.ThrowIfNullOrEmpty(cellId);
        _cellStates[cellId] = deltas ?? new Dictionary<string, string>();
    }

    public bool TryGetCellState(string cellId, out Dictionary<string, string>? deltas)
    {
        ArgumentException.ThrowIfNullOrEmpty(cellId);
        return _cellStates.TryGetValue(cellId, out deltas);
    }

    public void Clear()
    {
        _cellStates.Clear();
    }

    public object CaptureState()
    {
        // Convert to serializable WorldFlagsDto or custom JSON structure
        var flatFlags = new Dictionary<string, string>();
        foreach (var cellEntry in _cellStates)
        {
            foreach (var delta in cellEntry.Value)
            {
                // Flat key syntax: "cellId:objectKey" -> "value"
                flatFlags[$"{cellEntry.Key}:{delta.Key}"] = delta.Value;
            }
        }
        return new WorldFlagsDto(flatFlags);
    }

    public void RestoreState(object stateDto)
    {
        if (stateDto is WorldFlagsDto dto)
        {
            _cellStates.Clear();
            foreach (var flag in dto.Flags)
            {
                int colonIdx = flag.Key.IndexOf(':');
                if (colonIdx > 0)
                {
                    string cellId = flag.Key.Substring(0, colonIdx);
                    string objectKey = flag.Key.Substring(colonIdx + 1);

                    if (!_cellStates.TryGetValue(cellId, out var deltas))
                    {
                        deltas = new Dictionary<string, string>();
                        _cellStates[cellId] = deltas;
                    }
                    deltas[objectKey] = flag.Value;
                }
            }
        }
    }
}
