namespace Meridian.Environment;

/// <summary>
/// Interface for the global WeatherSystem.
/// Manages weather types, transition states, and the forecast queue.
/// </summary>
public interface IWeatherSystem
{
    string CurrentWeatherId { get; }
    float CurrentIntensity { get; }

    /// <summary>
    /// Transitions weather to a new state over time.
    /// </summary>
    void ChangeWeather(string weatherId, float intensity = 1.0f, float transitionDurationSeconds = 5.0f);

    /// <summary>
    /// Instantly forces a weather type.
    /// </summary>
    void ForceWeather(string weatherId, float intensity = 1.0f);
}

// Event payloads published to EventBus
public record struct WeatherChangedEvent(string OldWeatherId, string NewWeatherId);
public record struct WeatherTransitionStartedEvent(string TargetWeatherId, float DurationSeconds);
