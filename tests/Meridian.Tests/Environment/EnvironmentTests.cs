using Xunit;
using Meridian.Environment;

namespace Meridian.Tests.Environment;

public class EnvironmentTests
{
    [Fact]
    public void WorldClock_AdvanceTime_ShouldIncrementTimeCorrectly()
    {
        var clock = new WorldClock();

        clock.SetTime(8, 0);
        Assert.Equal(8, clock.CurrentHour);
        Assert.Equal(0, clock.CurrentMinute);
        Assert.Equal(TimePhase.Day, clock.CurrentPhase);

        clock.AdvanceTime(120);
        Assert.Equal(10, clock.CurrentHour);
        Assert.Equal(0, clock.CurrentMinute);
        Assert.Equal(TimePhase.Day, clock.CurrentPhase);

        clock.AdvanceTime(480);
        Assert.Equal(18, clock.CurrentHour);
        Assert.Equal(TimePhase.Dusk, clock.CurrentPhase);
    }

    [Fact]
    public void WorldClock_SetTimeScale_ShouldScaleAdvancement()
    {
        var clock = new WorldClock();
        clock.SetTime(8, 0);
        clock.SetTimeScale(2.0);

        clock.AdvanceTime(10); // 10 real minutes advance 20 game minutes

        Assert.Equal(8, clock.CurrentHour);
        Assert.Equal(20, clock.CurrentMinute);
    }

    [Fact]
    public void WorldClock_MidnightRollover_ShouldIncrementDayCounter()
    {
        var clock = new WorldClock();
        clock.SetTime(23, 30);
        int startDay = clock.DayCounter;

        clock.AdvanceTime(60); // cross midnight

        Assert.Equal(0, clock.CurrentHour);
        Assert.Equal(30, clock.CurrentMinute);
        Assert.Equal(startDay + 1, clock.DayCounter);
    }

    [Theory]
    [InlineData(4, TimePhase.Night)]
    [InlineData(5, TimePhase.Dawn)]
    [InlineData(8, TimePhase.Day)]
    [InlineData(17, TimePhase.Dusk)]
    [InlineData(20, TimePhase.Night)]
    public void WorldClock_PhaseBoundaries(int hour, TimePhase expected)
    {
        var clock = new WorldClock();
        clock.SetTime(hour, 0);
        Assert.Equal(expected, clock.CurrentPhase);
    }
}
