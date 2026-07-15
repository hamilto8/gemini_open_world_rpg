using Godot;
using System.Collections.Generic;
using Meridian.Core.Logic;

namespace Meridian.Data;

/// <summary>
/// Abstract editor-facing wrapper for the shared condition vocabulary (§3.6 item 1). Each concrete
/// subclass is a dumb data container: it exposes <c>[Export]</c> fields and maps them onto an engine-free
/// <see cref="ICondition"/> via <see cref="ToCondition"/>. All evaluation logic lives in
/// <c>Meridian.Core.Logic</c> so it can be unit-tested headlessly; these wrappers are not tested (they
/// cannot be instantiated outside the engine — ADR-0003).
/// </summary>
/// <remarks>
/// <c>FactionRepAtLeast</c> from the §3.6 list is deliberately omitted: no faction/reputation system
/// exists yet (§10.3). It is added as one new subclass here, plus a domain <c>FactionRepCondition</c>,
/// once that system lands — with no edits to the existing wrappers.
/// </remarks>
public abstract partial class ConditionResource : Resource
{
    /// <summary>Maps this resource's exported data onto an engine-free condition.</summary>
    public abstract ICondition ToCondition();
}

/// <summary>Wrapper for <see cref="TimeRangeCondition"/> (supports wrap-around ranges like 20→4).</summary>
[GlobalClass]
public partial class TimeRangeConditionResource : ConditionResource
{
    /// <summary>Inclusive start hour (0-23).</summary>
    [Export] public int StartHour { get; set; }

    /// <summary>Inclusive end hour (0-23); may be less than <see cref="StartHour"/> to wrap past midnight.</summary>
    [Export] public int EndHour { get; set; } = 23;

    /// <inheritdoc />
    public override ICondition ToCondition() => new TimeRangeCondition(StartHour, EndHour);
}

/// <summary>Wrapper for <see cref="WeatherIsCondition"/>.</summary>
[GlobalClass]
public partial class WeatherIsConditionResource : ConditionResource
{
    /// <summary>Weather id that must be active.</summary>
    [Export] public string WeatherId { get; set; } = "";

    /// <inheritdoc />
    public override ICondition ToCondition() => new WeatherIsCondition(WeatherId);
}

/// <summary>Wrapper for <see cref="StatCheckCondition"/> (deterministic threshold, §8.4).</summary>
[GlobalClass]
public partial class StatCheckConditionResource : ConditionResource
{
    /// <summary>Stat id to test.</summary>
    [Export] public string StatId { get; set; } = "";

    /// <summary>Inclusive minimum the stat must reach to pass.</summary>
    [Export] public float Minimum { get; set; }

    /// <inheritdoc />
    public override ICondition ToCondition() => new StatCheckCondition(StatId, Minimum);
}

/// <summary>Wrapper for <see cref="WorldFlagCondition"/>.</summary>
[GlobalClass]
public partial class WorldFlagConditionResource : ConditionResource
{
    /// <summary>Flag id to test.</summary>
    [Export] public string FlagId { get; set; } = "";

    /// <summary>Expected boolean value.</summary>
    [Export] public bool Expected { get; set; } = true;

    /// <inheritdoc />
    public override ICondition ToCondition() => new WorldFlagCondition(FlagId, Expected);
}

/// <summary>Wrapper for <see cref="HasItemCondition"/>.</summary>
[GlobalClass]
public partial class HasItemConditionResource : ConditionResource
{
    /// <summary>Item id to count.</summary>
    [Export] public string ItemId { get; set; } = "";

    /// <summary>Minimum quantity that must be held.</summary>
    [Export] public int MinCount { get; set; } = 1;

    /// <inheritdoc />
    public override ICondition ToCondition() => new HasItemCondition(ItemId, MinCount);
}

/// <summary>Wrapper for <see cref="IsInVehicleCondition"/>.</summary>
[GlobalClass]
public partial class IsInVehicleConditionResource : ConditionResource
{
    /// <inheritdoc />
    public override ICondition ToCondition() => new IsInVehicleCondition();
}

/// <summary>Wrapper for <see cref="AllOfCondition"/>. Null children are skipped; an empty set passes.</summary>
[GlobalClass]
public partial class AllOfConditionResource : ConditionResource
{
    /// <summary>Child conditions, all of which must pass.</summary>
    [Export] public Godot.Collections.Array<ConditionResource> Children { get; set; } = new();

    /// <inheritdoc />
    public override ICondition ToCondition() => new AllOfCondition(ConditionResourceMapper.Map(Children));
}

/// <summary>Wrapper for <see cref="AnyOfCondition"/>. Null children are skipped; an empty set fails.</summary>
[GlobalClass]
public partial class AnyOfConditionResource : ConditionResource
{
    /// <summary>Child conditions, at least one of which must pass.</summary>
    [Export] public Godot.Collections.Array<ConditionResource> Children { get; set; } = new();

    /// <inheritdoc />
    public override ICondition ToCondition() => new AnyOfCondition(ConditionResourceMapper.Map(Children));
}

/// <summary>Wrapper for <see cref="NotCondition"/>. A null inner condition evaluates to false.</summary>
[GlobalClass]
public partial class NotConditionResource : ConditionResource
{
    /// <summary>Condition to negate.</summary>
    [Export] public ConditionResource? Inner { get; set; }

    /// <inheritdoc />
    public override ICondition ToCondition() => new NotCondition(Inner?.ToCondition());
}

/// <summary>Shared mapping helper for composite wrappers.</summary>
internal static class ConditionResourceMapper
{
    /// <summary>Maps a Godot array of child resources to domain conditions, skipping null entries.</summary>
    public static List<ICondition> Map(Godot.Collections.Array<ConditionResource> children)
    {
        var list = new List<ICondition>();
        if (children is null)
        {
            return list;
        }

        foreach (var child in children)
        {
            if (child is not null)
            {
                list.Add(child.ToCondition());
            }
        }

        return list;
    }
}
