using System;
using System.Collections.Generic;
using System.Linq;
using Meridian.Core;
using Meridian.Data;

namespace Meridian.Quests;

public enum QuestState
{
    NotStarted,
    Active,
    Completed,
    Failed
}

/// <summary>
/// Domain model for Quest tracking and objective progress evaluation.
/// Decoupled from Godot for headless unit testing.
/// Enforces Section 14.1 and 14.3 requirements.
/// </summary>
public class QuestManager
{
    private readonly Dictionary<string, QuestState> _questStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, int>> _objectiveProgress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IQuestDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string, QuestState>? QuestStateChanged;
    public event Action<string, string, int>? ObjectiveProgressChanged;

    public IReadOnlyDictionary<string, QuestState> QuestStates => _questStates;

    public void RegisterQuest(IQuestDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.QuestId] = definition;
    }

    public QuestState GetQuestState(string questId)
    {
        return _questStates.TryGetValue(questId, out var state) ? state : QuestState.NotStarted;
    }

    public int GetObjectiveProgress(string questId, string objectiveId)
    {
        if (_objectiveProgress.TryGetValue(questId, out var progress) && progress.TryGetValue(objectiveId, out var count))
        {
            return count;
        }
        return 0;
    }

    public bool AcceptQuest(string questId)
    {
        if (!_definitions.TryGetValue(questId, out var def)) return false;
        if (GetQuestState(questId) != QuestState.NotStarted) return false;

        _questStates[questId] = QuestState.Active;
        _objectiveProgress[questId] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var objId in def.ObjectiveIds)
        {
            _objectiveProgress[questId][objId] = 0;
        }

        QuestStateChanged?.Invoke(questId, QuestState.Active);
        
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new QuestStateChangedEvent(questId, QuestState.Active));
        }

        return true;
    }

    public void IncrementObjective(string targetName, ObjectiveType type, int amount = 1)
    {
        foreach (var questEntry in _questStates.Where(q => q.Value == QuestState.Active))
        {
            string questId = questEntry.Key;
            var def = _definitions[questId];

            for (int i = 0; i < def.ObjectiveIds.Count; i++)
            {
                string objId = def.ObjectiveIds[i];
                ObjectiveType objType = def.ObjectiveTypes[i];
                string objTarget = def.ObjectiveTargets[i];
                int required = def.ObjectiveRequiredCounts[i];

                if (objType == type && objTarget.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    int current = GetObjectiveProgress(questId, objId);
                    if (current < required)
                    {
                        int next = Math.Min(required, current + amount);
                        _objectiveProgress[questId][objId] = next;

                        ObjectiveProgressChanged?.Invoke(questId, objId, next);

                        // Check if all objectives are complete
                        CheckQuestCompletion(questId);
                    }
                }
            }
        }
    }

    private void CheckQuestCompletion(string questId)
    {
        if (!_definitions.TryGetValue(questId, out var def)) return;
        
        bool allComplete = true;
        for (int i = 0; i < def.ObjectiveIds.Count; i++)
        {
            string objId = def.ObjectiveIds[i];
            int required = def.ObjectiveRequiredCounts[i];
            int current = GetObjectiveProgress(questId, objId);

            if (current < required)
            {
                allComplete = false;
                break;
            }
        }

        if (allComplete)
        {
            _questStates[questId] = QuestState.Completed;
            QuestStateChanged?.Invoke(questId, QuestState.Completed);

            if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
            {
                eventBus.Publish(new QuestStateChangedEvent(questId, QuestState.Completed));
            }
        }
    }
}

public record struct QuestStateChangedEvent(string QuestId, QuestState State);
