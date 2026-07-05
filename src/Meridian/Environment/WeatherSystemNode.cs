using Godot;
using Meridian.Core;

namespace Meridian.Environment;

/// <summary>
/// Autoload Node implementing WeatherSystem.
/// </summary>
public partial class WeatherSystemNode : Node, IWeatherSystem
{
    private string _currentWeatherId = "clear";
    private float _currentIntensity = 0.0f;

    private string? _targetWeatherId;
    private float _targetIntensity = 0.0f;
    private double _transitionTime = 0.0;
    private double _transitionDuration = 0.0;

    public string CurrentWeatherId => _currentWeatherId;
    public float CurrentIntensity => _currentIntensity;

    public override void _EnterTree()
    {
        Services.Register<IWeatherSystem>(this);
    }

    public override void _Process(double delta)
    {
        if (_targetWeatherId == null) return;

        _transitionTime += delta;
        if (_transitionTime >= _transitionDuration)
        {
            // Transition complete
            string old = _currentWeatherId;
            _currentWeatherId = _targetWeatherId;
            _currentIntensity = _targetIntensity;
            _targetWeatherId = null;

            if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
            {
                eventBus.Publish(new WeatherChangedEvent(old, _currentWeatherId));
            }
        }
        else
        {
            // Interpolate intensity if changing same weather, or just step transition
            double t = _transitionTime / _transitionDuration;
            _currentIntensity = (float)Mathf.Lerp(_currentIntensity, _targetIntensity, t);
        }
    }

    public void ChangeWeather(string weatherId, float intensity = 1.0f, float transitionDurationSeconds = 5.0f)
    {
        if (transitionDurationSeconds <= 0f)
        {
            ForceWeather(weatherId, intensity);
            return;
        }

        _targetWeatherId = weatherId;
        _targetIntensity = intensity;
        _transitionTime = 0.0;
        _transitionDuration = transitionDurationSeconds;

        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new WeatherTransitionStartedEvent(weatherId, transitionDurationSeconds));
        }
    }

    public void ForceWeather(string weatherId, float intensity = 1.0f)
    {
        string old = _currentWeatherId;
        _currentWeatherId = weatherId;
        _currentIntensity = intensity;
        _targetWeatherId = null;

        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new WeatherChangedEvent(old, _currentWeatherId));
        }
    }
}
