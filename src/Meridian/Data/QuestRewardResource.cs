using Godot;

namespace Meridian.Data;

/// <summary>
/// One reward granted on quest completion. Nested Resource (not parallel id/count arrays) so authors
/// can't desync the pair (Section 14.1, M6).
/// </summary>
[GlobalClass]
public partial class QuestRewardResource : Resource
{
    [Export] public string ItemId { get; set; } = "";
    [Export] public int Count { get; set; } = 1;
}
