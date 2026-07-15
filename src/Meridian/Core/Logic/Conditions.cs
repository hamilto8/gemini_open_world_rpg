using System.Collections.Generic;

namespace Meridian.Core.Logic;

// Concrete ICondition implementations for the shared vocabulary (§3.6 item 1):
//   WorldFlag, QuestState, TimeRange, WeatherIs, StatCheck, PlayerInRegion, HasItem, IsInVehicle,
//   and the AllOf / AnyOf / Not composites.
//
// Deferred: FactionRepAtLeast is intentionally NOT implemented — no faction/reputation system exists
// yet (§10.3 describes it as a future ReputationModel). It is added here as a new file + resource
// wrapper once that system lands, with zero changes to the existing conditions.
//
// Every condition is null/empty-argument tolerant: a meaningless argument makes Evaluate return false
// rather than throw, so malformed authored data can never crash evaluation.

/// <summary>
/// Passes when the context hour falls within the inclusive range [<c>startHour</c>, <c>endHour</c>].
/// Wrap-around ranges are supported: a 20→4 window matches 20,21,22,23,0,1,2,3,4 (§4.6, §9.3).
/// </summary>
public sealed class TimeRangeCondition : ICondition
{
    private readonly int _startHour;
    private readonly int _endHour;

    /// <summary>Creates a time-range condition. Hours are treated modulo 24.</summary>
    public TimeRangeCondition(int startHour, int endHour)
    {
        _startHour = startHour;
        _endHour = endHour;
    }

    /// <inheritdoc />
    public bool Evaluate(IConditionContext context)
    {
        if (context is null)
        {
            return false;
        }

        int hour = context.Hour;
        if (_startHour <= _endHour)
        {
            // Normal (non-wrapping) window.
            return hour >= _startHour && hour <= _endHour;
        }

        // Wrap-around window (e.g. 22→4): match the tail of one day or the head of the next.
        return hour >= _startHour || hour <= _endHour;
    }
}

/// <summary>Passes when the active weather id matches <c>weatherId</c> (case-insensitive) (§12).</summary>
public sealed class WeatherIsCondition : ICondition
{
    private readonly string? _weatherId;

    /// <summary>Creates a weather condition matching the given weather id.</summary>
    public WeatherIsCondition(string weatherId) => _weatherId = weatherId;

    /// <inheritdoc />
    public bool Evaluate(IConditionContext context)
    {
        if (context is null || string.IsNullOrEmpty(_weatherId))
        {
            return false;
        }

        return string.Equals(context.CurrentWeatherId, _weatherId, System.StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Passes when faction reputation reaches the authored threshold.</summary>
public sealed class FactionReputationCondition : ICondition
{
    private readonly string? _factionId;
    private readonly int _minimum;

    public FactionReputationCondition(string factionId, int minimum)
    {
        _factionId = factionId;
        _minimum = minimum;
    }

    public bool Evaluate(IConditionContext context) =>
        context is not null
        && !string.IsNullOrEmpty(_factionId)
        && context.GetFactionReputation(_factionId) >= _minimum;
}

/// <summary>
/// Passes when a stat is at or above <c>minimum</c>. Uses the deterministic-threshold policy
/// recommended in §8.4 (legible, Witcher-style) rather than a random roll.
/// </summary>
public sealed class StatCheckCondition : ICondition
{
    private readonly string? _statId;
    private readonly float _minimum;

    /// <summary>Creates a stat check that passes when <c>statId</c> &gt;= <c>minimum</c>.</summary>
    public StatCheckCondition(string statId, float minimum)
    {
        _statId = statId;
        _minimum = minimum;
    }

    /// <inheritdoc />
    public bool Evaluate(IConditionContext context)
    {
        if (context is null || string.IsNullOrEmpty(_statId))
        {
            return false;
        }

        return context.GetStat(_statId) >= _minimum;
    }
}

/// <summary>Passes when a world flag equals the <c>expected</c> boolean value (§3.6, §9.5).</summary>
public sealed class WorldFlagCondition : ICondition
{
    private readonly string? _flagId;
    private readonly bool _expected;

    /// <summary>Creates a world-flag condition.</summary>
    public WorldFlagCondition(string flagId, bool expected)
    {
        _flagId = flagId;
        _expected = expected;
    }

    /// <inheritdoc />
    public bool Evaluate(IConditionContext context)
    {
        if (context is null || string.IsNullOrEmpty(_flagId))
        {
            return false;
        }

        return context.GetWorldFlag(_flagId) == _expected;
    }
}

/// <summary>Passes when the player holds at least <c>minCount</c> of an item (§7.4).</summary>
public sealed class HasItemCondition : ICondition
{
    private readonly string? _itemId;
    private readonly int _minCount;

    /// <summary>Creates a has-item condition. <c>minCount</c> defaults to a sensible 1 at the call site.</summary>
    public HasItemCondition(string itemId, int minCount)
    {
        _itemId = itemId;
        _minCount = minCount;
    }

    /// <inheritdoc />
    public bool Evaluate(IConditionContext context)
    {
        if (context is null || string.IsNullOrEmpty(_itemId))
        {
            return false;
        }

        return context.GetItemCount(_itemId) >= _minCount;
    }
}

/// <summary>Passes when the player is currently possessing a vehicle (§6.6, §11).</summary>
public sealed class IsInVehicleCondition : ICondition
{
    /// <inheritdoc />
    public bool Evaluate(IConditionContext context) => context is not null && context.IsInVehicle;
}

/// <summary>
/// Passes when a quest is in a specific state (compared by the enum's string form, e.g. "Active").
/// A null/unknown quest state never matches (§9.1).
/// </summary>
public sealed class QuestStateCondition : ICondition
{
    private readonly string? _questId;
    private readonly string? _state;

    /// <summary>Creates a quest-state condition.</summary>
    public QuestStateCondition(string questId, string state)
    {
        _questId = questId;
        _state = state;
    }

    /// <inheritdoc />
    public bool Evaluate(IConditionContext context)
    {
        if (context is null || string.IsNullOrEmpty(_questId) || string.IsNullOrEmpty(_state))
        {
            return false;
        }

        string? actual = context.GetQuestState(_questId);
        return actual is not null && string.Equals(actual, _state, System.StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Passes when the player is currently inside the given region (§4).</summary>
public sealed class PlayerInRegionCondition : ICondition
{
    private readonly string? _regionId;

    /// <summary>Creates a region-membership condition.</summary>
    public PlayerInRegionCondition(string regionId) => _regionId = regionId;

    /// <inheritdoc />
    public bool Evaluate(IConditionContext context)
    {
        if (context is null || string.IsNullOrEmpty(_regionId))
        {
            return false;
        }

        return string.Equals(context.CurrentRegionId, _regionId, System.StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Composite that passes only when every child passes. An empty set evaluates to <c>true</c>
/// (vacuous truth); null children are skipped. Pinned by domain tests.
/// </summary>
public sealed class AllOfCondition : ICondition
{
    private readonly IReadOnlyList<ICondition> _children;

    /// <summary>Creates an all-of composite. A null list is treated as empty.</summary>
    public AllOfCondition(IReadOnlyList<ICondition> children) => _children = children ?? System.Array.Empty<ICondition>();

    /// <inheritdoc />
    public bool Evaluate(IConditionContext context)
    {
        foreach (var child in _children)
        {
            if (child is null)
            {
                continue;
            }

            if (!child.Evaluate(context))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Composite that passes when at least one child passes. An empty set evaluates to <c>false</c>;
/// null children are skipped. Pinned by domain tests.
/// </summary>
public sealed class AnyOfCondition : ICondition
{
    private readonly IReadOnlyList<ICondition> _children;

    /// <summary>Creates an any-of composite. A null list is treated as empty.</summary>
    public AnyOfCondition(IReadOnlyList<ICondition> children) => _children = children ?? System.Array.Empty<ICondition>();

    /// <inheritdoc />
    public bool Evaluate(IConditionContext context)
    {
        foreach (var child in _children)
        {
            if (child is null)
            {
                continue;
            }

            if (child.Evaluate(context))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Inverts a child condition. A null inner condition evaluates to <c>false</c> (null-argument
/// tolerance) rather than throwing or inverting nothing.
/// </summary>
public sealed class NotCondition : ICondition
{
    private readonly ICondition? _inner;

    /// <summary>Creates a negation of the supplied condition.</summary>
    public NotCondition(ICondition? inner) => _inner = inner;

    /// <inheritdoc />
    public bool Evaluate(IConditionContext context)
    {
        if (context is null || _inner is null)
        {
            return false;
        }

        return !_inner.Evaluate(context);
    }
}
