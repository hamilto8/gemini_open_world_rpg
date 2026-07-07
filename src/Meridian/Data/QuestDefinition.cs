using Godot;

namespace Meridian.Data;

/// <summary>
/// Data-driven definition Resource for a single Quest.
/// Enforces Section 14.1 requirements.
/// </summary>
[GlobalClass]
public partial class QuestDefinition : Resource
{
    [Export] public string QuestId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public string Description { get; set; } = "";

    [Export] public Godot.Collections.Array<QuestObjectiveResource> Objectives { get; set; } = new();
    [Export] public Godot.Collections.Array<QuestRewardResource> Rewards { get; set; } = new();
}
