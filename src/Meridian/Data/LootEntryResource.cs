using Godot;

namespace Meridian.Data;

/// <summary>
/// One weighted entry in a <see cref="LootTableResource"/>. Modeling entries as nested Resources
/// (rather than parallel arrays) makes it impossible for content authors to desync per-field lengths
/// (Section 7.4, M6).
/// </summary>
[GlobalClass]
public partial class LootEntryResource : Resource
{
    [Export] public string ItemId { get; set; } = "";
    [Export] public int Weight { get; set; } = 1;
    [Export] public int MinQuantity { get; set; } = 1;
    [Export] public int MaxQuantity { get; set; } = 1;
}
