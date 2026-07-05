using System;
using Godot;
using Meridian.Core;
using Meridian.Core.Save;

namespace Meridian.Environment;

/// <summary>
/// Autoload Node wrapper for WorldClock. Manages time advancement via Godot's lifecycle.
/// </summary>
public partial class WorldClockNode : Node, IWorldClock, ISaveParticipant
{
    [Export] public double DayLengthRealMinutes { get; set; } = 40.0;

    private readonly WorldClock _clock = new();
    private double _timeScale = 1.0;

    public double TotalGameMinutes => _clock.TotalGameMinutes;
    public int DayCounter => _clock.DayCounter;
    public int CurrentHour => _clock.CurrentHour;
    public int CurrentMinute => _clock.CurrentMinute;
    public TimePhase CurrentPhase => _clock.CurrentPhase;

    private int _lastMinute = -1;
    private int _lastHour = -1;
    private int _lastDay = -1;
    private TimePhase _lastPhase = TimePhase.Night;

    public string ParticipantId => "TimeWeather";
    public int RestoreOrder => 20;

    public override void _EnterTree()
    {
        Services.Register<IWorldClock>(this);
    }

    public override void _Ready()
    {
        if (Services.TryGet<ISaveService>(out var saveService) && saveService != null)
        {
            saveService.RegisterParticipant(this);
        }

        _lastMinute = CurrentMinute;
        _lastHour = CurrentHour;
        _lastDay = DayCounter;
        _lastPhase = CurrentPhase;
    }

    public override void _ExitTree()
    {
        if (Services.TryGet<ISaveService>(out var saveService) && saveService != null)
        {
            saveService.UnregisterParticipant(this);
        }
    }

    public override void _Process(double delta)
    {
        double gameMinutesPerRealSecond = 1440.0 / (DayLengthRealMinutes * 60.0);
        double minutesToAdvance = delta * gameMinutesPerRealSecond * _timeScale;

        AdvanceTime(minutesToAdvance);
    }

    public void SetTime(int hour, int minute)
    {
        _clock.SetTime(hour, minute);
        EvaluateTimeChanges();
    }

    public void SetTimeScale(double scale)
    {
        _timeScale = scale;
        _clock.SetTimeScale(scale);
    }

    public void AdvanceTime(double minutes)
    {
        _clock.AdvanceTime(minutes);
        EvaluateTimeChanges();
    }

    private void EvaluateTimeChanges()
    {
        if (!Services.TryGet<IEventBus>(out var eventBus) || eventBus == null) return;

        int minute = CurrentMinute;
        int hour = CurrentHour;
        int day = DayCounter;
        TimePhase phase = CurrentPhase;

        if (minute != _lastMinute)
        {
            _lastMinute = minute;
            eventBus.Publish(new MinuteTickEvent(hour, minute));
        }

        if (hour != _lastHour)
        {
            _lastHour = hour;
            eventBus.Publish(new HourChangedEvent(hour));
        }

        if (phase != _lastPhase)
        {
            _lastPhase = phase;
            eventBus.Publish(new PhaseChangedEvent(phase));
        }

        if (day != _lastDay)
        {
            _lastDay = day;
            eventBus.Publish(new DayChangedEvent(day));
        }
    }

    public object CaptureState()
    {
        string weatherId = "clear";
        float intensity = 0f;
        float elapsed = 0f;
        int seed = 1234;

        if (Services.TryGet<IWeatherSystem>(out var ws) && ws != null)
        {
            weatherId = ws.CurrentWeatherId;
            intensity = ws.CurrentIntensity;
        }

        return new TimeWeatherDto(
            TotalGameMinutes: TotalGameMinutes,
            DayCounter: DayCounter,
            CurrentWeatherId: weatherId,
            WeatherIntensity: intensity,
            WeatherElapsed: elapsed,
            ForecastSeed: seed
        );
    }

    public void RestoreState(object stateDto)
    {
        if (stateDto is TimeWeatherDto dto)
        {
            _clock.ForceTotalMinutes(dto.TotalGameMinutes);
            _lastMinute = CurrentMinute;
            _lastHour = CurrentHour;
            _lastDay = DayCounter;
            _lastPhase = CurrentPhase;

            if (Services.TryGet<IWeatherSystem>(out var ws) && ws != null)
            {
                ws.ForceWeather(dto.CurrentWeatherId, dto.WeatherIntensity);
            }
        }
    }
}
