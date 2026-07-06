namespace Meridian.NPC;

/// <summary>
/// Pure C# scheduling domain logic for NPC activities. Decoupled from Godot.
/// Enforces Section 3.2 and 15.2 requirements.
/// </summary>
public class NpcScheduler
{
    public NpcActivityState EvaluateState(int hour)
    {
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
}
