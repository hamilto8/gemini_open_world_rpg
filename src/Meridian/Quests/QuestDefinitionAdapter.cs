using System;
using System.Collections.Generic;
using Meridian.Data;
using Meridian.Core.Logic;

namespace Meridian.Quests;

/// <summary>
/// Adapter wrapping the Godot <see cref="QuestDefinition"/> Resource to implement the domain
/// <see cref="IQuestDefinition"/> interface. Projects the nested objective/reward Resources into
/// engine-free records the QuestManager consumes.
/// </summary>
public class QuestDefinitionAdapter : IQuestDefinition
{
    private readonly QuestDefinition _resource;
    private readonly List<QuestObjective> _objectives = new();
    private readonly List<QuestReward> _rewards = new();
    private readonly List<ICondition> _startConditions = new();
    private readonly List<IGameAction> _onAcceptActions = new();
    private readonly List<IGameAction> _onCompleteActions = new();

    public string QuestId => _resource.QuestId;
    public string DisplayName => _resource.DisplayName;
    public string Description => _resource.Description;

    public IReadOnlyList<QuestObjective> Objectives => _objectives;
    public IReadOnlyList<QuestReward> Rewards => _rewards;
    public IReadOnlyList<ICondition> StartConditions => _startConditions;
    public IReadOnlyList<IGameAction> OnAcceptActions => _onAcceptActions;
    public IReadOnlyList<IGameAction> OnCompleteActions => _onCompleteActions;

    public QuestDefinitionAdapter(QuestDefinition resource)
    {
        _resource = resource ?? throw new ArgumentNullException(nameof(resource));

        foreach (var objective in _resource.Objectives)
        {
            if (objective != null)
            {
                _objectives.Add(new QuestObjective(objective.ObjectiveId, objective.Type, objective.Target, objective.RequiredCount));
            }
        }

        foreach (var reward in _resource.Rewards)
        {
            if (reward != null)
            {
                _rewards.Add(new QuestReward(reward.ItemId, reward.Count));
            }
        }

        foreach (var condition in _resource.StartConditions)
        {
            if (condition is not null)
            {
                _startConditions.Add(condition.ToCondition());
            }
        }

        MapActions(_resource.OnAcceptActions, _onAcceptActions);
        MapActions(_resource.OnCompleteActions, _onCompleteActions);
    }

    private static void MapActions(
        Godot.Collections.Array<GameActionResource> resources,
        ICollection<IGameAction> destination)
    {
        foreach (var action in resources)
        {
            if (action is not null)
            {
                destination.Add(action.ToAction());
            }
        }
    }
}
