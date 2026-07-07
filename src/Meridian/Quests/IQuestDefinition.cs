using System.Collections.Generic;

namespace Meridian.Quests;

/// <summary>One quest objective as a single cohesive record (no parallel arrays to desync — M6).</summary>
public readonly record struct QuestObjective(string ObjectiveId, ObjectiveType Type, string Target, int RequiredCount);

/// <summary>One quest reward: an item id and a quantity to grant on completion.</summary>
public readonly record struct QuestReward(string ItemId, int Count);

/// <summary>
/// Interface representing quest definitions required by the QuestManager.
/// Allows unit tests to mock quest goals without instantiating Godot Resource classes.
/// </summary>
public interface IQuestDefinition
{
    string QuestId { get; }
    string DisplayName { get; }
    string Description { get; }

    /// <summary>Objectives modeled as cohesive records rather than parallel arrays.</summary>
    IReadOnlyList<QuestObjective> Objectives { get; }

    /// <summary>Items granted when every objective is complete.</summary>
    IReadOnlyList<QuestReward> Rewards { get; }
}
