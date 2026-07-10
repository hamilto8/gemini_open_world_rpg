using Godot;

namespace Meridian.Data.Indexes;

/// <summary>
/// Master index of movement profiles (§19.1). The MovementProfiles registry loads from this at boot. A dumb
/// data container by design.
/// </summary>
[GlobalClass]
public partial class MovementProfileIndex : Resource
{
    /// <summary>Every registered movement profile.</summary>
    [Export] public Godot.Collections.Array<MovementProfile> Definitions { get; set; } = new();
}
