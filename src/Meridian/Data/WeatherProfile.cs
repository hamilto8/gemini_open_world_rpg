using Godot;

namespace Meridian.Data;

/// <summary>
/// Data-driven definition Resource for a Weather state profile.
/// Implements <see cref="IWeatherProfile"/> for registry/validator decoupling (ADR-0003).
/// Enforces Section 11.4 requirements.
/// </summary>
[GlobalClass]
public partial class WeatherProfile : Resource, IWeatherProfile
{
    [Export] public string WeatherId { get; set; } = "clear";

    // The registry keys weather profiles by their WeatherId (§19.9).
    string IWeatherProfile.Id => WeatherId;
    [Export] public float FogDensity { get; set; } = 0.001f;
    [Export] public Color FogColor { get; set; } = Colors.SkyBlue;
    [Export] public Color LightColor { get; set; } = Colors.White;

    [Export] public float MoveSpeedModifier { get; set; } = 0f; // negative value for slow, e.g. -0.15f for 15% slow in rain
}
