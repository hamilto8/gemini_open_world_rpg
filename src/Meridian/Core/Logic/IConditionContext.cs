namespace Meridian.Core.Logic;

/// <summary>
/// Read-only view of world state that <see cref="ICondition"/> evaluators query (§3.6 item 1).
/// The interface is deliberately engine-free (primitives and strings only) so conditions and their
/// fakes can be exercised in headless unit tests. Production wiring is supplied by
/// <see cref="ServicesConditionContext"/>, which pulls the underlying services lazily.
/// </summary>
public interface IConditionContext
{
    /// <summary>Current in-game hour, 0-23 (from <c>IWorldClock.CurrentHour</c>).</summary>
    int Hour { get; }

    /// <summary>
    /// Current day/night phase as a string (e.g. "Dawn", "Day", "Dusk", "Night"). String form keeps
    /// <c>Meridian.Core.Logic</c> decoupled from <c>Meridian.Environment.TimePhase</c>.
    /// </summary>
    string CurrentPhase { get; }

    /// <summary>Id of the currently active weather state, or null if unknown/none.</summary>
    string? CurrentWeatherId { get; }

    /// <summary>Returns the boolean value of a world flag; absent flags read as <c>false</c>.</summary>
    bool GetWorldFlag(string id);

    /// <summary>Returns the modified value of a player stat; unknown stats read as <c>0</c>.</summary>
    float GetStat(string id);

    /// <summary>Returns how many of an item the player currently holds; unknown ids read as <c>0</c>.</summary>
    int GetItemCount(string id);

    /// <summary>True when the player currently possesses a vehicle rather than the on-foot avatar.</summary>
    bool IsInVehicle { get; }

    /// <summary>Id of the region the player is currently in, or null if none is active.</summary>
    string? CurrentRegionId { get; }

    /// <summary>
    /// Returns the string form of a quest's state (e.g. "NotStarted", "Active", "Completed", "Failed"),
    /// or null when the quest is unknown / no quest system is available. String form keeps Core decoupled
    /// from the <c>Meridian.Quests.QuestState</c> enum.
    /// </summary>
    string? GetQuestState(string questId);
}
