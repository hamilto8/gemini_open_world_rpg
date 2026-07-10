using Godot;

namespace Meridian.Data.Indexes;

/// <summary>
/// Master index of weather profiles (§19.1). The WeatherProfiles registry loads from this at boot. A dumb
/// data container by design.
/// </summary>
[GlobalClass]
public partial class WeatherIndex : Resource
{
    /// <summary>Every registered weather profile.</summary>
    [Export] public Godot.Collections.Array<WeatherProfile> Definitions { get; set; } = new();
}
