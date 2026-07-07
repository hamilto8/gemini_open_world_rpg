using Godot;
using Meridian.Quests;

namespace Meridian.Data;

/// <summary>
/// One objective within a <see cref="QuestDefinition"/>. Nested Resources keep each objective's
/// fields together so authors can't desync parallel arrays (Section 14.1, M6).
/// </summary>
[GlobalClass]
public partial class QuestObjectiveResource : Resource
{
    [Export] public string ObjectiveId { get; set; } = "";
    [Export] public ObjectiveType Type { get; set; } = ObjectiveType.GatherItem;
    [Export] public string Target { get; set; } = ""; // e.g. "npc_merchant", "metal_scrap"
    [Export] public int RequiredCount { get; set; } = 1;
}
