using System.Collections.Generic;
using Meridian.Data;

namespace Meridian.Quests;

/// <summary>
/// Interface representing quest definitions required by the QuestManager.
/// Allows unit tests to mock quest goals without instantiating Godot Resource classes.
/// </summary>
public interface IQuestDefinition
{
    string QuestId { get; }
    string DisplayName { get; }
    string Description { get; }
    IReadOnlyList<string> ObjectiveIds { get; }
    IReadOnlyList<ObjectiveType> ObjectiveTypes { get; }
    IReadOnlyList<string> ObjectiveTargets { get; }
    IReadOnlyList<int> ObjectiveRequiredCounts { get; }
}

/// <summary>
/// Basic mock implementation of IQuestDefinition for unit testing.
/// </summary>
public class BasicQuestDefinition : IQuestDefinition
{
    public string QuestId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public IReadOnlyList<string> ObjectiveIds { get; set; } = new List<string>();
    public IReadOnlyList<ObjectiveType> ObjectiveTypes { get; set; } = new List<ObjectiveType>();
    public IReadOnlyList<string> ObjectiveTargets { get; set; } = new List<string>();
    public IReadOnlyList<int> ObjectiveRequiredCounts { get; set; } = new List<int>();
}
