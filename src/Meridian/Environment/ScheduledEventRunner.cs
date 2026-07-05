using System;
using System.Collections.Generic;

namespace Meridian.Environment;

/// <summary>
/// Domain utility managing time-based scheduled events (recurring daily or one-shot).
/// Evaluates on minute ticks. Decoupled from Godot for unit testing.
/// Enforces Section 11.2 and 11.3 requirements.
/// </summary>
public class ScheduledEventRunner
{
    private readonly List<ScheduledEvent> _events = new();

    public IReadOnlyList<ScheduledEvent> Events => _events;

    public void RegisterDailyEvent(int hour, int minute, Action callback)
    {
        _events.Add(new ScheduledEvent(hour, minute, callback, isRecurring: true));
    }

    public void RegisterOneShotEvent(int hour, int minute, Action callback)
    {
        _events.Add(new ScheduledEvent(hour, minute, callback, isRecurring: false));
    }

    public void Evaluate(int hour, int minute)
    {
        for (int i = _events.Count - 1; i >= 0; i--)
        {
            var ev = _events[i];
            if (ev.Hour == hour && ev.Minute == minute)
            {
                try
                {
                    ev.Callback?.Invoke();
                }
                catch (Exception ex)
                {
                    // Fail-safe execution (Section 11.2)
                    System.Diagnostics.Debug.WriteLine($"[ScheduledEventRunner] Event execution failed: {ex.Message}");
                }

                if (!ev.IsRecurring)
                {
                    _events.RemoveAt(i);
                }
            }
        }
    }
}

public class ScheduledEvent
{
    public int Hour { get; }
    public int Minute { get; }
    public Action Callback { get; }
    public bool IsRecurring { get; }

    public ScheduledEvent(int hour, int minute, Action callback, bool isRecurring)
    {
        Hour = hour;
        Minute = minute;
        Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        IsRecurring = isRecurring;
    }
}
