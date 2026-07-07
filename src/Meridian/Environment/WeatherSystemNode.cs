using Godot;
using System.Collections.Generic;
using Meridian.Core;
using Meridian.Data;

namespace Meridian.Environment;

/// <summary>
/// Autoload Node implementing the global <see cref="IWeatherSystem"/> service:
/// weather state machine plus the stat-modifier pushes weather applies to the possessed player
/// (Section 12). Visual blending is delegated to a scene-layer controller (Section 3.4).
/// </summary>
public partial class WeatherSystemNode : Node, IWeatherSystem
{
    [Export] public WeatherProfile? DefaultWeather { get; set; }

    /// <summary>
    /// Weather profiles this system can transition to, keyed at boot by <c>WeatherId</c>.
    /// Content registers weather types here (or, later, via a WeatherIndex resource).
    /// </summary>
    [Export] public Godot.Collections.Array<WeatherProfile> AvailableProfiles { get; set; } = new();

    private readonly Dictionary<string, WeatherProfile> _profilesById = new();
    private WeatherProfile? _currentWeather;
    private float _currentIntensity = 1.0f;
    private Modifier? _activeWeatherModifier;

    public WeatherProfile? CurrentWeather => _currentWeather;
    public string CurrentWeatherId => _currentWeather?.WeatherId ?? "clear";
    public float CurrentIntensity => _currentWeather == null ? 0f : _currentIntensity;

    public override void _EnterTree()
    {
        Services.Register<IWeatherSystem>(this);
    }

    public override void _Ready()
    {
        IndexProfiles();

        if (DefaultWeather != null)
        {
            _currentWeather = DefaultWeather;
            ApplyWeatherModifiers();
        }
    }

    public override void _ExitTree()
    {
        RemoveWeatherModifiers();

        if (Services.TryGet<IWeatherSystem>(out var current) && ReferenceEquals(current, this))
        {
            Services.Unregister<IWeatherSystem>();
        }
    }

    private void IndexProfiles()
    {
        _profilesById.Clear();
        if (DefaultWeather != null)
        {
            _profilesById[DefaultWeather.WeatherId] = DefaultWeather;
        }
        foreach (var profile in AvailableProfiles)
        {
            if (profile != null)
            {
                _profilesById[profile.WeatherId] = profile;
            }
        }
    }

    /// <summary>Transitions weather to a new state. Duration is reserved for scene-layer visual blending.</summary>
    public void ChangeWeather(string weatherId, float intensity = 1.0f, float transitionDurationSeconds = 5.0f)
        => ApplyWeather(weatherId, intensity, transitionDurationSeconds);

    /// <summary>Instantly forces a weather type (used on save-restore and by the debug console).</summary>
    public void ForceWeather(string weatherId, float intensity = 1.0f)
        => ApplyWeather(weatherId, intensity, 0f);

    private void ApplyWeather(string weatherId, float intensity, float transitionDurationSeconds)
    {
        if (!_profilesById.TryGetValue(weatherId, out var profile))
        {
            GD.PushWarning($"[WeatherSystem] Unknown weather id '{weatherId}'; transition ignored.");
            return;
        }

        string oldId = CurrentWeatherId;
        intensity = Mathf.Clamp(intensity, 0f, 1f);
        if (oldId == weatherId && Mathf.IsEqualApprox(_currentIntensity, intensity))
        {
            return;
        }

        RemoveWeatherModifiers();
        _currentWeather = profile;
        _currentIntensity = intensity;
        ApplyWeatherModifiers();

        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            if (transitionDurationSeconds > 0f)
            {
                eventBus.Publish(new WeatherTransitionStartedEvent(weatherId, transitionDurationSeconds));
            }
            eventBus.Publish(new WeatherChangedEvent(oldId, weatherId));
        }
    }

    private void ApplyWeatherModifiers()
    {
        if (_currentWeather == null || _currentWeather.MoveSpeedModifier == 0f)
        {
            return;
        }

        // Find the possessed avatar's StatBlock and push a weather modifier (Section 12).
        if (Services.TryGet<IPlayerController>(out var pc) && pc?.PossessedEntity is Node avatarNode)
        {
            var stats = avatarNode.GetNodeOrNull<StatBlockNode>("StatBlock");
            if (stats != null)
            {
                // MoveSpeedModifier is authored as a fraction (-0.15f = 15% slower); apply as a
                // percent modifier scaled by the current weather intensity (Section 12, one Modifier System).
                _activeWeatherModifier = new Modifier(
                    targetStatId: "move_speed",
                    operation: ModifierOp.PercentAdd,
                    value: _currentWeather.MoveSpeedModifier * _currentIntensity,
                    sourceTag: $"weather_{_currentWeather.WeatherId}"
                );

                stats.AddModifier(_activeWeatherModifier);
            }
        }
    }

    private void RemoveWeatherModifiers()
    {
        if (_activeWeatherModifier == null)
        {
            return;
        }

        if (Services.TryGet<IPlayerController>(out var pc) && pc?.PossessedEntity is Node avatarNode)
        {
            var stats = avatarNode.GetNodeOrNull<StatBlockNode>("StatBlock");
            stats?.RemoveModifier(_activeWeatherModifier);
        }

        _activeWeatherModifier = null;
    }
}
