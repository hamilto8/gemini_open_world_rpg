using System;

namespace Meridian.Environment;

/// <summary>
/// Pure C# implementation of WorldClock logic. Decoupled from Godot for headless testing.
/// </summary>
public class WorldClock : IWorldClock
{
    private double _totalGameMinutes = 480.0; // Start at 08:00 AM (8 hours * 60 minutes)
    private double _timeScale = 1.0;

    public double TotalGameMinutes => _totalGameMinutes;
    public int DayCounter => (int)(_totalGameMinutes / 1440.0) + 1;
    public int CurrentHour => (int)(_totalGameMinutes % 1440.0) / 60;
    public int CurrentMinute => (int)(_totalGameMinutes % 1440.0) % 60;

    public TimePhase CurrentPhase
    {
        get
        {
            int hour = CurrentHour;
            if (hour >= 5 && hour < 8) return TimePhase.Dawn;
            if (hour >= 8 && hour < 17) return TimePhase.Day;
            if (hour >= 17 && hour < 20) return TimePhase.Dusk;
            return TimePhase.Night;
        }
    }

    public void SetTime(int hour, int minute)
    {
        int day = (int)(_totalGameMinutes / 1440.0);
        _totalGameMinutes = (day * 1440.0) + (hour * 60.0) + minute;
    }

    public void SetTimeScale(double scale)
    {
        _timeScale = scale;
    }

    public void AdvanceTime(double minutes)
    {
        _totalGameMinutes += minutes;
    }

    public void ForceTotalMinutes(double minutes)
    {
        _totalGameMinutes = minutes;
    }
}
