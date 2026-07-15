using System;
using System.Collections.Generic;
using System.Linq;
using Meridian.Core;
using Meridian.Core.Save;
using Meridian.Items;
using Meridian.Core.Registry;
using Meridian.Core.Logic;

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
/// Decoupled from Godot for headless unit testing. Participates in save/restore.
/// Enforces Section 14.1 and 14.3 requirements.
/// </summary>
public class QuestManager : ISaveParticipant
{
    private readonly Dictionary<string, QuestState> _questStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, int>> _objectiveProgress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IQuestDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IConditionContext? _conditionContext;
    private readonly IActionContext? _actionContext;

    public event Action<string, QuestState>? QuestStateChanged;
    public event Action<string, string, int>? ObjectiveProgressChanged;

    public IReadOnlyDictionary<string, QuestState> QuestStates => _questStates;

    public string ParticipantId => "Quests";
    public int RestoreOrder => 50;
    public Type StateType => typeof(QuestSaveDto);

    public QuestManager(IConditionContext? conditionContext = null, IActionContext? actionContext = null)
    {
        _conditionContext = conditionContext;
        _actionContext = actionContext;
    }

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
        if (def.StartConditions.Count > 0 && (_conditionContext is null || !AllConditionsPass(def.StartConditions)))
        {
            return false;
        }

        _questStates[questId] = QuestState.Active;
        _objectiveProgress[questId] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var objective in def.Objectives)
        {
            _objectiveProgress[questId][objective.ObjectiveId] = 0;
        }

        QuestStateChanged?.Invoke(questId, QuestState.Active);
        ExecuteActions(def.OnAcceptActions);

        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new QuestStateChangedEvent(questId, QuestState.Active));
        }

        return true;
    }

    public void IncrementObjective(string targetName, ObjectiveType type, int amount = 1)
    {
        // Snapshot active quest ids: completing a quest mutates _questStates, so iterating the live
        // collection would be a mutation-during-enumeration hazard (M5).
        var activeQuestIds = _questStates
            .Where(q => q.Value == QuestState.Active)
            .Select(q => q.Key)
            .ToList();

        foreach (var questId in activeQuestIds)
        {
            if (!_definitions.TryGetValue(questId, out var def)) continue;

            foreach (var objective in def.Objectives)
            {
                if (objective.Type != type || !objective.Target.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int current = GetObjectiveProgress(questId, objective.ObjectiveId);
                if (current < objective.RequiredCount)
                {
                    int next = Math.Min(objective.RequiredCount, current + amount);
                    _objectiveProgress[questId][objective.ObjectiveId] = next;

                    ObjectiveProgressChanged?.Invoke(questId, objective.ObjectiveId, next);
                    CheckQuestCompletion(questId);
                }
            }
        }
    }

    private void CheckQuestCompletion(string questId)
    {
        if (!_definitions.TryGetValue(questId, out var def)) return;

        foreach (var objective in def.Objectives)
        {
            if (GetObjectiveProgress(questId, objective.ObjectiveId) < objective.RequiredCount)
            {
                return; // not all objectives complete
            }
        }

        _questStates[questId] = QuestState.Completed;
        GrantRewards(def);
        ExecuteActions(def.OnCompleteActions);

        QuestStateChanged?.Invoke(questId, QuestState.Completed);

        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new QuestStateChangedEvent(questId, QuestState.Completed));
        }
    }

    private bool AllConditionsPass(IReadOnlyList<ICondition> conditions)
    {
        foreach (var condition in conditions)
        {
            if (condition is null || !condition.Evaluate(_conditionContext!))
            {
                return false;
            }
        }

        return true;
    }

    private void ExecuteActions(IReadOnlyList<IGameAction> actions)
    {
        if (_actionContext is null)
        {
            return;
        }

        foreach (var action in actions)
        {
            action?.Execute(_actionContext);
        }
    }

    private static void GrantRewards(IQuestDefinition def)
    {
        if (def.Rewards.Count == 0) return;
        if (!Services.TryGet<IInventoryProvider>(out var provider) || provider == null) return;
        if (!Services.TryGet<IContentDatabase>(out var database) || database == null) return;

        foreach (var reward in def.Rewards)
        {
            if (!string.IsNullOrEmpty(reward.ItemId) && reward.Count > 0
                && database.Items.TryGet(reward.ItemId, out var definition) && definition != null)
            {
                provider.Inventory.RegisterDefinition(definition);
                provider.Inventory.AddItem(new ItemInstance(reward.ItemId, reward.Count));
            }
        }
    }

    public object CaptureState()
    {
        var states = _questStates.ToDictionary(kv => kv.Key, kv => kv.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var progress = _objectiveProgress.ToDictionary(
            kv => kv.Key,
            kv => new Dictionary<string, int>(kv.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        return new QuestSaveDto(states, progress);
    }

    public void RestoreState(object stateDto)
    {
        if (stateDto is not QuestSaveDto dto) return;

        _questStates.Clear();
        foreach (var kv in dto.QuestStates)
        {
            if (Enum.TryParse<QuestState>(kv.Value, out var state))
            {
                _questStates[kv.Key] = state;
            }
        }

        _objectiveProgress.Clear();
        foreach (var kv in dto.ObjectiveProgress)
        {
            _objectiveProgress[kv.Key] = new Dictionary<string, int>(kv.Value, StringComparer.OrdinalIgnoreCase);
        }
    }
}

public record struct QuestStateChangedEvent(string QuestId, QuestState State);
