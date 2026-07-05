using System;
using System.IO;
using Xunit;
using Meridian.Core;
using Meridian.Core.Save;
using Meridian.Environment;

namespace Meridian.Tests.Environment;

public class EnvironmentTests : IDisposable
{
    public EnvironmentTests()
    {
        Services.Reset();
        Services.Register<ISaveService>(new SaveService(Path.GetTempPath()));
    }

    public void Dispose()
    {
        Services.Reset();
    }

    [Fact]
    public void WorldClock_AdvanceTime_ShouldIncrementTimeCorrectly()
    {
        var clock = new WorldClock();
        
        // Initial setup at 08:00 AM (480 minutes)
        clock.SetTime(8, 0);
        Assert.Equal(8, clock.CurrentHour);
        Assert.Equal(0, clock.CurrentMinute);
        Assert.Equal(TimePhase.Day, clock.CurrentPhase);

        // Advance 120 minutes (should be 10:00 AM)
        clock.AdvanceTime(120);
        Assert.Equal(10, clock.CurrentHour);
        Assert.Equal(0, clock.CurrentMinute);
        Assert.Equal(TimePhase.Day, clock.CurrentPhase);

        // Advance 480 minutes (should be 06:00 PM / 18:00 - Dusk phase)
        clock.AdvanceTime(480);
        Assert.Equal(18, clock.CurrentHour);
        Assert.Equal(TimePhase.Dusk, clock.CurrentPhase);
    }
}
