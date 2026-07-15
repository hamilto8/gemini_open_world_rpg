using Godot;
using Meridian.Core;

namespace Meridian.Environment;

/// <summary>
/// Scene-layer day/night and weather presentation. Simulation remains in the clock/weather services;
/// this replaceable component only drives authored Godot lighting/environment resources.
/// </summary>
public partial class EnvironmentPresentationController : Node
{
    [Export] public NodePath SunPath { get; set; } = "../../DirectionalLight3D";
    [Export] public NodePath WorldEnvironmentPath { get; set; } = "../../WorldEnvironment";
    [Export(PropertyHint.Range, "0.1,30.0,0.1")] public float WeatherBlendSeconds { get; set; } = 5f;

    [ExportGroup("Day Phase Colors")]
    [Export] public Color DawnColor { get; set; } = new("d99972");
    [Export] public Color DayColor { get; set; } = new("fff4dc");
    [Export] public Color DuskColor { get; set; } = new("df8b68");
    [Export] public Color NightColor { get; set; } = new("6f83b5");

    [ExportGroup("Lighting")]
    [Export(PropertyHint.Range, "0.0,4.0,0.05")] public float DayEnergy { get; set; } = 1.15f;
    [Export(PropertyHint.Range, "0.0,2.0,0.05")] public float NightEnergy { get; set; } = 0.08f;
    [Export(PropertyHint.Range, "0.0,2.0,0.05")] public float DayAmbientEnergy { get; set; } = 0.65f;
    [Export(PropertyHint.Range, "0.0,2.0,0.05")] public float NightAmbientEnergy { get; set; } = 0.18f;

    private DirectionalLight3D? _sun;
    private WorldEnvironment? _worldEnvironment;
    private Color _weatherLight = Colors.White;
    private Color _weatherFog = Colors.SkyBlue;
    private float _weatherFogDensity;

    public override void _Ready()
    {
        _sun = GetNodeOrNull<DirectionalLight3D>(SunPath);
        _worldEnvironment = GetNodeOrNull<WorldEnvironment>(WorldEnvironmentPath);
        Apply(0d, immediate: true);
    }

    public override void _Process(double delta)
    {
        Apply(delta, immediate: false);
    }

    private void Apply(double delta, bool immediate)
    {
        if (!Services.TryGet<IWorldClock>(out var clock) || clock == null) return;

        float dayT = (float)((clock.TotalGameMinutes % 1440d) / 1440d);
        if (dayT < 0f) dayT += 1f;

        Color daylight = SampleDaylight(dayT);
        float sunAmount = Mathf.Clamp(Mathf.Sin((dayT - 0.25f) * Mathf.Tau) * 0.5f + 0.5f, 0f, 1f);
        ReadWeatherTargets(out Color targetLight, out Color targetFog, out float targetDensity);
        float blend = immediate || WeatherBlendSeconds <= 0f
            ? 1f
            : 1f - Mathf.Exp(-(float)delta / WeatherBlendSeconds * 4.6f);
        _weatherLight = _weatherLight.Lerp(targetLight, blend);
        _weatherFog = _weatherFog.Lerp(targetFog, blend);
        _weatherFogDensity = Mathf.Lerp(_weatherFogDensity, targetDensity, blend);

        if (_sun != null)
        {
            _sun.RotationDegrees = new Vector3(dayT * 360f - 90f, -25f, 0f);
            _sun.LightColor = daylight * _weatherLight;
            _sun.LightEnergy = Mathf.Lerp(NightEnergy, DayEnergy, sunAmount);
        }

        Godot.Environment? environment = _worldEnvironment?.Environment;
        if (environment == null) return;

        environment.AmbientLightColor = daylight.Lerp(_weatherLight, 0.35f);
        environment.AmbientLightEnergy = Mathf.Lerp(NightAmbientEnergy, DayAmbientEnergy, sunAmount);
        environment.FogEnabled = _weatherFogDensity > 0.0001f;
        environment.FogDensity = _weatherFogDensity;
        environment.FogLightColor = _weatherFog;

        if (environment.Sky?.SkyMaterial is ProceduralSkyMaterial sky)
        {
            Color horizon = daylight.Lerp(_weatherFog, 0.45f);
            sky.SkyHorizonColor = horizon;
            sky.SkyTopColor = daylight.Darkened(0.42f).Lerp(_weatherLight, 0.2f);
            sky.GroundHorizonColor = horizon.Darkened(0.25f);
        }
    }

    private static void ReadWeatherTargets(out Color light, out Color fog, out float density)
    {
        if (Services.TryGet<IWeatherSystem>(out var weather)
            && weather is WeatherSystemNode node
            && node.CurrentWeather != null)
        {
            light = Colors.White.Lerp(node.CurrentWeather.LightColor, weather.CurrentIntensity);
            fog = node.CurrentWeather.FogColor;
            density = node.CurrentWeather.FogDensity * weather.CurrentIntensity;
            return;
        }

        light = Colors.White;
        fog = Colors.SkyBlue;
        density = 0f;
    }

    private Color SampleDaylight(float t)
    {
        // Authored phase colors with smooth, continuous boundaries: night→dawn→day→dusk→night.
        if (t < 5f / 24f) return NightColor;
        if (t < 8f / 24f) return NightColor.Lerp(DawnColor, Smooth((t - 5f / 24f) * 8f));
        if (t < 10f / 24f) return DawnColor.Lerp(DayColor, Smooth((t - 8f / 24f) * 12f));
        if (t < 17f / 24f) return DayColor;
        if (t < 20f / 24f) return DayColor.Lerp(DuskColor, Smooth((t - 17f / 24f) * 8f));
        if (t < 22f / 24f) return DuskColor.Lerp(NightColor, Smooth((t - 20f / 24f) * 12f));
        return NightColor;
    }

    private static float Smooth(float value)
    {
        value = Mathf.Clamp(value, 0f, 1f);
        return value * value * (3f - 2f * value);
    }
}
