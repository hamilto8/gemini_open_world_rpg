using Godot;

namespace Meridian.Data.Indexes;

/// <summary>
/// Master index of loot tables (§19.1). The LootTables registry loads from this at boot. A dumb data
/// container by design.
/// </summary>
[GlobalClass]
public partial class LootTableIndex : Resource
{
    /// <summary>Every registered loot table.</summary>
    [Export] public Godot.Collections.Array<LootTableResource> Definitions { get; set; } = new();
}
