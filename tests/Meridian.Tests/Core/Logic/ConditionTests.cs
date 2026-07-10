using System.Collections.Generic;
using Xunit;
using Meridian.Core.Logic;

namespace Meridian.Tests.Core.Logic;

public class ConditionTests
{
    private static FakeConditionContext Context() => new();

    // --- TimeRangeCondition ---

    [Theory]
    [InlineData(8, 18, 8, true)]   // inclusive start
    [InlineData(8, 18, 18, true)]  // inclusive end
    [InlineData(8, 18, 12, true)]
    [InlineData(8, 18, 7, false)]
    [InlineData(8, 18, 19, false)]
    public void TimeRange_NonWrapping_MatchesInclusiveWindow(int start, int end, int hour, bool expected)
    {
        var ctx = Context();
        ctx.Hour = hour;
        Assert.Equal(expected, new TimeRangeCondition(start, end).Evaluate(ctx));
    }

    [Theory]
    [InlineData(23, true)]  // tail of the evening
    [InlineData(2, true)]   // head of the morning
    [InlineData(22, true)]  // inclusive start
    [InlineData(4, true)]   // inclusive end
    [InlineData(12, false)] // midday is outside 22->4
    [InlineData(5, false)]
    public void TimeRange_WrapAround_MatchesAcrossMidnight(int hour, bool expected)
    {
        var ctx = Context();
        ctx.Hour = hour;
        Assert.Equal(expected, new TimeRangeCondition(22, 4).Evaluate(ctx));
    }

    [Fact]
    public void TimeRange_NullContext_IsFalse()
    {
        Assert.False(new TimeRangeCondition(0, 23).Evaluate(null!));
    }

    // --- WeatherIsCondition ---

    [Fact]
    public void WeatherIs_MatchingId_IsTrue_CaseInsensitive()
    {
        var ctx = Context();
        ctx.CurrentWeatherId = "Storm";
        Assert.True(new WeatherIsCondition("storm").Evaluate(ctx));
    }

    [Fact]
    public void WeatherIs_DifferentOrNullWeather_IsFalse()
    {
        var ctx = Context();
        ctx.CurrentWeatherId = "clear";
        Assert.False(new WeatherIsCondition("storm").Evaluate(ctx));

        ctx.CurrentWeatherId = null;
        Assert.False(new WeatherIsCondition("storm").Evaluate(ctx));
    }

    [Fact]
    public void WeatherIs_NullOrEmptyArgument_IsFalse_NeverThrows()
    {
        var ctx = Context();
        ctx.CurrentWeatherId = "storm";
        Assert.False(new WeatherIsCondition(null!).Evaluate(ctx));
        Assert.False(new WeatherIsCondition("").Evaluate(ctx));
        Assert.False(new WeatherIsCondition("storm").Evaluate(null!));
    }

    // --- StatCheckCondition (deterministic threshold, §8.4) ---

    [Theory]
    [InlineData(6f, 6f, true)]   // exactly at threshold passes
    [InlineData(7f, 6f, true)]
    [InlineData(5.9f, 6f, false)]
    public void StatCheck_ThresholdIsDeterministicAndInclusive(float value, float minimum, bool expected)
    {
        var ctx = Context();
        ctx.Stats["tech"] = value;
        Assert.Equal(expected, new StatCheckCondition("tech", minimum).Evaluate(ctx));
    }

    [Fact]
    public void StatCheck_UnknownStatReadsZero()
    {
        var ctx = Context();
        Assert.False(new StatCheckCondition("mystery", 1f).Evaluate(ctx));
        Assert.True(new StatCheckCondition("mystery", 0f).Evaluate(ctx)); // 0 >= 0
    }

    [Fact]
    public void StatCheck_NullArguments_IsFalse_NeverThrows()
    {
        Assert.False(new StatCheckCondition(null!, 1f).Evaluate(Context()));
        Assert.False(new StatCheckCondition("", 1f).Evaluate(Context()));
        Assert.False(new StatCheckCondition("tech", 1f).Evaluate(null!));
    }

    // --- WorldFlagCondition ---

    [Fact]
    public void WorldFlag_MatchesExpectedValue()
    {
        var ctx = Context();
        ctx.Flags["betrayed_kel"] = true;

        Assert.True(new WorldFlagCondition("betrayed_kel", expected: true).Evaluate(ctx));
        Assert.False(new WorldFlagCondition("betrayed_kel", expected: false).Evaluate(ctx));
    }

    [Fact]
    public void WorldFlag_AbsentFlagReadsFalse()
    {
        var ctx = Context();
        Assert.False(new WorldFlagCondition("never_set", expected: true).Evaluate(ctx));
        Assert.True(new WorldFlagCondition("never_set", expected: false).Evaluate(ctx));
    }

    [Fact]
    public void WorldFlag_NullArguments_IsFalse_NeverThrows()
    {
        Assert.False(new WorldFlagCondition(null!, true).Evaluate(Context()));
        Assert.False(new WorldFlagCondition("", true).Evaluate(Context()));
        Assert.False(new WorldFlagCondition("f", true).Evaluate(null!));
    }

    // --- HasItemCondition ---

    [Theory]
    [InlineData(2, 2, true)]
    [InlineData(3, 2, true)]
    [InlineData(1, 2, false)]
    [InlineData(0, 1, false)]
    public void HasItem_ComparesCountAgainstMinimum(int held, int min, bool expected)
    {
        var ctx = Context();
        ctx.ItemCounts["medkit"] = held;
        Assert.Equal(expected, new HasItemCondition("medkit", min).Evaluate(ctx));
    }

    [Fact]
    public void HasItem_NullArguments_IsFalse_NeverThrows()
    {
        Assert.False(new HasItemCondition(null!, 1).Evaluate(Context()));
        Assert.False(new HasItemCondition("", 1).Evaluate(Context()));
        Assert.False(new HasItemCondition("medkit", 1).Evaluate(null!));
    }

    // --- IsInVehicleCondition ---

    [Fact]
    public void IsInVehicle_ReflectsContextState()
    {
        var ctx = Context();
        Assert.False(new IsInVehicleCondition().Evaluate(ctx));

        ctx.IsInVehicle = true;
        Assert.True(new IsInVehicleCondition().Evaluate(ctx));
    }

    [Fact]
    public void IsInVehicle_NullContext_IsFalse()
    {
        Assert.False(new IsInVehicleCondition().Evaluate(null!));
    }

    // --- QuestStateCondition ---

    [Fact]
    public void QuestState_MatchesStateName_CaseInsensitive()
    {
        var ctx = Context();
        ctx.QuestStates["courier_gambit"] = "Active";

        Assert.True(new QuestStateCondition("courier_gambit", "active").Evaluate(ctx));
        Assert.False(new QuestStateCondition("courier_gambit", "Completed").Evaluate(ctx));
    }

    [Fact]
    public void QuestState_UnknownQuest_IsFalse()
    {
        Assert.False(new QuestStateCondition("unknown_quest", "Active").Evaluate(Context()));
    }

    [Fact]
    public void QuestState_NullArguments_IsFalse_NeverThrows()
    {
        Assert.False(new QuestStateCondition(null!, "Active").Evaluate(Context()));
        Assert.False(new QuestStateCondition("q", null!).Evaluate(Context()));
        Assert.False(new QuestStateCondition("q", "Active").Evaluate(null!));
    }

    // --- PlayerInRegionCondition ---

    [Fact]
    public void PlayerInRegion_MatchesCurrentRegion_CaseInsensitive()
    {
        var ctx = Context();
        ctx.CurrentRegionId = "Harbor_Town";

        Assert.True(new PlayerInRegionCondition("harbor_town").Evaluate(ctx));
        Assert.False(new PlayerInRegionCondition("northern_pines").Evaluate(ctx));
    }

    [Fact]
    public void PlayerInRegion_NoActiveRegionOrNullArgs_IsFalse()
    {
        var ctx = Context();
        ctx.CurrentRegionId = null;
        Assert.False(new PlayerInRegionCondition("harbor_town").Evaluate(ctx));
        Assert.False(new PlayerInRegionCondition(null!).Evaluate(Context()));
        Assert.False(new PlayerInRegionCondition("").Evaluate(Context()));
        Assert.False(new PlayerInRegionCondition("harbor_town").Evaluate(null!));
    }

    // --- Composites ---

    private sealed class ConstCondition : ICondition
    {
        private readonly bool _value;
        public ConstCondition(bool value) => _value = value;
        public bool Evaluate(IConditionContext context) => _value;
    }

    [Fact]
    public void AllOf_TrueOnlyWhenEveryChildPasses()
    {
        var ctx = Context();
        Assert.True(new AllOfCondition(new ICondition[] { new ConstCondition(true), new ConstCondition(true) }).Evaluate(ctx));
        Assert.False(new AllOfCondition(new ICondition[] { new ConstCondition(true), new ConstCondition(false) }).Evaluate(ctx));
    }

    [Fact]
    public void AnyOf_TrueWhenAtLeastOneChildPasses()
    {
        var ctx = Context();
        Assert.True(new AnyOfCondition(new ICondition[] { new ConstCondition(false), new ConstCondition(true) }).Evaluate(ctx));
        Assert.False(new AnyOfCondition(new ICondition[] { new ConstCondition(false), new ConstCondition(false) }).Evaluate(ctx));
    }

    [Fact]
    public void EmptyComposites_AllOfIsTrue_AnyOfIsFalse()
    {
        // Pins the empty-composite semantics the resource wrappers rely on (skip-null children).
        var ctx = Context();
        Assert.True(new AllOfCondition(new List<ICondition>()).Evaluate(ctx));
        Assert.False(new AnyOfCondition(new List<ICondition>()).Evaluate(ctx));
    }

    [Fact]
    public void Composites_NullListAndNullChildren_AreTolerated()
    {
        var ctx = Context();
        Assert.True(new AllOfCondition(null!).Evaluate(ctx));
        Assert.False(new AnyOfCondition(null!).Evaluate(ctx));

        // Null children are skipped, not evaluated.
        Assert.True(new AllOfCondition(new ICondition?[] { null, new ConstCondition(true) }!).Evaluate(ctx));
        Assert.False(new AnyOfCondition(new ICondition?[] { null }!).Evaluate(ctx));
    }

    [Fact]
    public void Not_InvertsChild_AndNullInnerIsFalse()
    {
        var ctx = Context();
        Assert.False(new NotCondition(new ConstCondition(true)).Evaluate(ctx));
        Assert.True(new NotCondition(new ConstCondition(false)).Evaluate(ctx));
        Assert.False(new NotCondition(null).Evaluate(ctx));
        Assert.False(new NotCondition(new ConstCondition(false)).Evaluate(null!));
    }
}
