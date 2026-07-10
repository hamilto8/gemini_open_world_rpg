using Godot;

namespace Meridian.Data.Indexes;

/// <summary>
/// Master index of vehicle handling profiles (§19.1). The HandlingProfiles registry loads from this at
/// boot. A dumb data container by design.
/// </summary>
[GlobalClass]
public partial class HandlingProfileIndex : Resource
{
    /// <summary>Every registered vehicle handling profile.</summary>
    [Export] public Godot.Collections.Array<HandlingProfile> Definitions { get; set; } = new();
}
