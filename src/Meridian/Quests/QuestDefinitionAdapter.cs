using System;
using System.Collections.Generic;
using System.Linq;
using Meridian.Data;

namespace Meridian.Quests;

/// <summary>
/// Adapter class wrapping the Godot QuestDefinition Resource to implement the domain IQuestDefinition interface.
/// Prevents Godot C# source generator failures on explicit interface properties in partial classes.
/// </summary>
public class QuestDefinitionAdapter : IQuestDefinition
{
    private readonly QuestDefinition _resource;

    public string QuestId => _resource.QuestId;
    public string DisplayName => _resource.DisplayName;
    public string Description => _resource.Description;

    public IReadOnlyList<string> ObjectiveIds => _resource.ObjectiveIds;
    public IReadOnlyList<ObjectiveType> ObjectiveTypes => _resource.ObjectiveTypes;
    public IReadOnlyList<string> ObjectiveTargets => _resource.ObjectiveTargets;
    public IReadOnlyList<int> ObjectiveRequiredCounts => _resource.ObjectiveRequiredCounts;

    public QuestDefinitionAdapter(QuestDefinition resource)
    {
        _resource = resource ?? throw new ArgumentNullException(nameof(resource));
    }
}
