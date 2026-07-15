namespace Meridian.NPC;

/// <summary>
/// Pure C# scheduling domain logic for NPC activities. Decoupled from Godot.
/// Enforces Section 3.2 and 15.2 requirements.
/// </summary>
public class NpcScheduler
{
    private readonly System.Collections.Generic.IReadOnlyList<NpcScheduleEntry>? _schedule;

    public NpcScheduler(System.Collections.Generic.IReadOnlyList<NpcScheduleEntry>? schedule = null)
    {
        _schedule = schedule;
    }

    public NpcActivityState EvaluateState(int hour)
    {
        if (TryEvaluateEntry(hour, out var entry))
        {
            return entry.Activity;
        }

        // 08:00 to 17:00 -> Work
        // 17:00 to 22:00 -> Tavern
        // 22:00 to 08:00 -> Sleep
        if (hour >= 8 && hour < 17)
        {
            return NpcActivityState.Working;
        }
        else if (hour >= 17 && hour < 22)
        {
            return NpcActivityState.Socializing;
        }
        else
        {
            return NpcActivityState.Sleeping;
        }
    }

    public bool TryEvaluateEntry(int hour, out NpcScheduleEntry entry)
    {
        if (_schedule is not null)
        {
            int normalizedHour = ((hour % 24) + 24) % 24;
            foreach (var candidate in _schedule)
            {
                bool matches = candidate.StartHour <= candidate.EndHour
                    ? normalizedHour >= candidate.StartHour && normalizedHour <= candidate.EndHour
                    : normalizedHour >= candidate.StartHour || normalizedHour <= candidate.EndHour;
                if (matches)
                {
                    entry = candidate;
                    return true;
                }
            }
        }

        entry = default;
        return false;
    }
}
