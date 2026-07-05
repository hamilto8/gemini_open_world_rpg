using Godot;

namespace Meridian.Data;

/// <summary>
/// Data-driven definition Resource for a Weather state profile.
/// Enforces Section 11.4 requirements.
/// </summary>
[GlobalClass]
public partial class WeatherProfile : Resource
{
    [Export] public string WeatherId { get; set; } = "clear";
    [Export] public float FogDensity { get; set; } = 0.001f;
    [Export] public Color FogColor { get; set; } = Colors.SkyBlue;
    [Export] public Color LightColor { get; set; } = Colors.White;

    [Export] public float MoveSpeedModifier { get; set; } = 0f; // negative value for slow, e.g. -0.15f for 15% slow in rain
}
