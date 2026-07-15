using System;
using System.Collections.Generic;
using Meridian.Core.Save;

namespace Meridian.Factions;

public interface IFactionReputationService
{
    int GetReputation(string factionId);
    bool ModifyReputation(string factionId, int amount);
}

/// <summary>Runtime reputation state initialized from the typed faction registry.</summary>
public sealed class FactionReputationService : IFactionReputationService, ISaveParticipant
{
    private readonly Dictionary<string, IFactionDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _reputation = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string, int>? ReputationChanged;
    public string ParticipantId => "FactionReputation";
    public int RestoreOrder => SaveRestoreOrder.Narrative;
    public Type StateType => typeof(FactionStateDto);

    public FactionReputationService(IEnumerable<IFactionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        foreach (var definition in definitions)
        {
            if (definition is null || string.IsNullOrEmpty(definition.Id) || _definitions.ContainsKey(definition.Id))
            {
                continue;
            }

            _definitions.Add(definition.Id, definition);
            _reputation.Add(
                definition.Id,
                Math.Clamp(definition.StartingReputation, definition.MinimumReputation, definition.MaximumReputation));
        }
    }

    public int GetReputation(string factionId) =>
        !string.IsNullOrEmpty(factionId) && _reputation.TryGetValue(factionId, out int value) ? value : 0;

    public bool ModifyReputation(string factionId, int amount)
    {
        if (string.IsNullOrEmpty(factionId) || !_definitions.TryGetValue(factionId, out var definition))
        {
            return false;
        }

        int previous = _reputation[factionId];
        int next = Math.Clamp(previous + amount, definition.MinimumReputation, definition.MaximumReputation);
        if (next == previous)
        {
            return true;
        }

        _reputation[factionId] = next;
        ReputationChanged?.Invoke(factionId, next);
        return true;
    }

    public object CaptureState()
        => new FactionStateDto(new Dictionary<string, int>(_reputation, StringComparer.OrdinalIgnoreCase));

    public void RestoreState(object stateDto)
    {
        if (stateDto is not FactionStateDto state)
        {
            throw new ArgumentException("Expected faction reputation state.", nameof(stateDto));
        }

        _reputation.Clear();
        foreach (var (id, value) in state.Reputation ?? new Dictionary<string, int>())
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            int restored = _definitions.TryGetValue(id, out var definition)
                ? Math.Clamp(value, definition.MinimumReputation, definition.MaximumReputation)
                : value;
            _reputation[id] = restored;
            ReputationChanged?.Invoke(id, restored);
        }
    }
}
