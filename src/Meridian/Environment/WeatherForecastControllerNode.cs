using Godot;
using System;
using System.Collections.Generic;
using Meridian.Core;
using Meridian.Core.Registry;
using Meridian.Core.Save;
using Meridian.Data;

namespace Meridian.Environment;

/// <summary>Clock-driven deterministic weather forecast assembled from WeatherProfile transition edges.</summary>
public partial class WeatherForecastControllerNode : Node, ISaveParticipant
{
    [Export] public uint ForecastSeed { get; set; } = 20260714u;
    [Export(PropertyHint.Range, "1,10080,1")] public int InitialDurationGameMinutes { get; set; } = 180;

    private readonly List<WeatherForecastChoice> _choices = new();
    private DeterministicWeatherForecast? _forecast;
    private IDisposable? _minuteSubscription;
    private int _remainingMinutes;

    public int RemainingMinutes => _remainingMinutes;
    public uint RandomState => _forecast?.State ?? ForecastSeed;
    public string ParticipantId => "WeatherForecast";
    public int RestoreOrder => SaveRestoreOrder.Environment + 1;
    public Type StateType => typeof(WeatherForecastStateDto);

    public override void _Ready()
    {
        _forecast = new DeterministicWeatherForecast(ForecastSeed);
        _remainingMinutes = Math.Max(1, InitialDurationGameMinutes);
        if (Services.TryGet<IEventBus>(out var bus) && bus != null)
        {
            _minuteSubscription = bus.Subscribe<MinuteTickEvent>(OnMinuteTick);
        }
        if (Services.TryGet<ISaveService>(out var saveService) && saveService != null)
        {
            saveService.RegisterParticipant(this);
        }
    }

    public override void _ExitTree()
    {
        _minuteSubscription?.Dispose();
        _minuteSubscription = null;
        if (Services.TryGet<ISaveService>(out var saveService) && saveService != null)
        {
            saveService.UnregisterParticipant(this);
        }
    }

    public void RestoreForecast(int remainingMinutes, uint randomState)
    {
        _remainingMinutes = Math.Max(1, remainingMinutes);
        _forecast ??= new DeterministicWeatherForecast(ForecastSeed);
        _forecast.RestoreState(randomState);
    }

    public object CaptureState()
    {
        string weatherId = Services.TryGet<IWeatherSystem>(out var weather) && weather != null
            ? weather.CurrentWeatherId
            : "clear";
        return new WeatherForecastStateDto(weatherId, _remainingMinutes, RandomState);
    }

    public void RestoreState(object stateDto)
    {
        if (stateDto is not WeatherForecastStateDto state)
        {
            throw new ArgumentException("Expected weather forecast state.", nameof(stateDto));
        }

        RestoreForecast(state.RemainingMinutes, state.RandomState);
        if (Services.TryGet<IWeatherSystem>(out var weather) && weather != null
            && !string.IsNullOrWhiteSpace(state.CurrentWeatherId))
        {
            weather.ForceWeather(state.CurrentWeatherId, weather.CurrentIntensity <= 0f ? 1f : weather.CurrentIntensity);
        }
    }

    private void OnMinuteTick(MinuteTickEvent _)
    {
        if (--_remainingMinutes > 0 || _forecast == null) return;
        if (!Services.TryGet<IWeatherSystem>(out var weather) || weather == null) return;

        BuildChoices(weather.CurrentWeatherId);
        WeatherForecastSelection next = _forecast.Select(_choices, weather.CurrentWeatherId);
        _remainingMinutes = next.DurationMinutes;
        weather.ChangeWeather(next.WeatherId, next.Intensity, next.TransitionSeconds);
    }

    private void BuildChoices(string weatherId)
    {
        _choices.Clear();
        if (!Services.TryGet<IContentDatabase>(out var content)
            || content == null
            || !content.WeatherProfiles.TryGet(weatherId, out var definition)
            || definition is not WeatherProfile profile)
        {
            return;
        }

        foreach (WeatherTransitionResource transition in profile.Transitions)
        {
            if (transition == null || string.IsNullOrWhiteSpace(transition.TargetWeatherId)) continue;
            _choices.Add(new WeatherForecastChoice(
                transition.TargetWeatherId,
                transition.Weight,
                transition.Intensity,
                transition.MinDurationGameMinutes,
                transition.MaxDurationGameMinutes,
                transition.TransitionSeconds));
        }
    }
}
