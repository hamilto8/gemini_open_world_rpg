using Godot;

namespace Meridian.Data.Indexes;

/// <summary>
/// Master index of item definitions (§19.1). The Items registry loads from this at boot; adding an item =
/// drop the .tres and add one entry here — zero code (§1.5.7). A dumb data container by design.
/// </summary>
[GlobalClass]
public partial class ItemIndex : Resource
{
    /// <summary>Every registered item definition.</summary>
    [Export] public Godot.Collections.Array<ItemResource> Definitions { get; set; } = new();
}
