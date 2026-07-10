using Godot;

namespace Meridian.Data.Indexes;

/// <summary>
/// Master index of weapon definitions (§19.1). The Weapons registry loads from this at boot. A dumb data
/// container by design.
/// </summary>
[GlobalClass]
public partial class WeaponIndex : Resource
{
    /// <summary>Every registered weapon definition.</summary>
    [Export] public Godot.Collections.Array<WeaponResource> Definitions { get; set; } = new();
}
