using Godot;

namespace Meridian.Data.Indexes;

/// <summary>
/// Master index of region definitions (§19.1). The Regions registry loads from this at boot. A dumb data
/// container by design.
/// </summary>
[GlobalClass]
public partial class RegionIndex : Resource
{
    /// <summary>Every registered region definition.</summary>
    [Export] public Godot.Collections.Array<RegionDefinition> Definitions { get; set; } = new();
}
