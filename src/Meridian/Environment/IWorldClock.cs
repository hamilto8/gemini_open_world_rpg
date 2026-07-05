namespace Meridian.Environment;

/// <summary>
/// Predefined phases of the day/night cycle.
/// </summary>
public enum TimePhase
{
    Dawn,
    Day,
    Dusk,
    Night
}

/// <summary>
/// Interface for the global WorldClock.
/// Owns game time, speed scaling, and broadcasts time tick/change events.
/// </summary>
public interface IWorldClock
{
    double TotalGameMinutes { get; }
    int DayCounter { get; }
    int CurrentHour { get; }
    int CurrentMinute { get; }
    TimePhase CurrentPhase { get; }

    void SetTime(int hour, int minute);
    void SetTimeScale(double scale);
    void AdvanceTime(double minutes);
}

// Event payloads published to EventBus
public record struct MinuteTickEvent(int Hour, int Minute);
public record struct HourChangedEvent(int Hour);
public record struct PhaseChangedEvent(TimePhase NewPhase);
public record struct DayChangedEvent(int NewDay);
