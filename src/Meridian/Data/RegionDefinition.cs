using Godot;

namespace Meridian.Data;

/// <summary>
/// Data-driven definition Resource for a World Region.
/// Enforces Section 4.2 requirements.
/// </summary>
[GlobalClass]
public partial class RegionDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public float CellSize { get; set; } = 256.0f; // Dimension of one tile in meters
    [Export] public Rect2I Bounds { get; set; } = new Rect2I(0, 0, 4, 4); // Grid coordinate span
    [Export] public int StreamPriority { get; set; } = 10;

    [Export] public Godot.Collections.Array<CellDefinition> Cells { get; set; } = new();
}
