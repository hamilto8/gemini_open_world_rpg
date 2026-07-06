using Godot;
using Meridian.Quests;

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

    [Export] public Godot.Collections.Array<string> ObjectiveIds { get; set; } = new();
    [Export] public Godot.Collections.Array<ObjectiveType> ObjectiveTypes { get; set; } = new();
    [Export] public Godot.Collections.Array<string> ObjectiveTargets { get; set; } = new(); // e.g. "npc_merchant", "metal_scrap"
    [Export] public Godot.Collections.Array<int> ObjectiveRequiredCounts { get; set; } = new();

    [Export] public Godot.Collections.Array<string> RewardItemIds { get; set; } = new();
    [Export] public Godot.Collections.Array<int> RewardItemCounts { get; set; } = new();
}
