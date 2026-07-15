using System;
using Xunit;
using Meridian.Core;
using Meridian.Environment;

namespace Meridian.Tests.Environment;

public class WeatherTests
{
    [Fact]
    public void ScheduledEventRunner_ShouldExecuteRegisteredEvents()
    {
        var runner = new ScheduledEventRunner();
        bool oneShotFired = false;
        bool recurringFired = false;

        runner.RegisterOneShotEvent(8, 0, () => oneShotFired = true);
        runner.RegisterDailyEvent(18, 30, () => recurringFired = true);

        // Advance to 8:00
        runner.Evaluate(8, 0);
        Assert.True(oneShotFired);

        // Advance to 18:30
        runner.Evaluate(18, 30);
        Assert.True(recurringFired);

        // Verify one-shot is removed (does not fire again)
        oneShotFired = false;
        runner.Evaluate(8, 0);
        Assert.False(oneShotFired);
    }

    [Fact]
    public void Weather_StateModifiers_ShouldApplyToStatBlock()
    {
        var stats = new StatBlock();
        stats.SetBaseStat("move_speed", 10.0f);
        Assert.Equal(10.0f, stats.GetStat("move_speed"));

        // Rain applies a -15% slow as a percent modifier (fraction convention, matches WeatherSystemNode).
        var rainModifier = new Modifier(
            targetStatId: "move_speed",
            operation: ModifierOp.PercentAdd,
            value: -0.15f,
            sourceTag: "weather_rain"
        );

        stats.AddModifier(rainModifier);
        Assert.Equal(8.5f, stats.GetStat("move_speed"));

        // Clear weather
        stats.RemoveModifier(rainModifier);
        Assert.Equal(10.0f, stats.GetStat("move_speed"));
    }

    [Fact]
    public void Forecast_ShouldBeDeterministicAndRestorable()
    {
        WeatherForecastChoice[] choices =
        {
            new("clear", 0.5f, 1f, 60, 120, 5f),
            new("rain", 0.5f, 0.8f, 30, 90, 10f),
        };
        var first = new DeterministicWeatherForecast(42);
        var second = new DeterministicWeatherForecast(42);

        WeatherForecastSelection firstSelection = first.Select(choices, "clear");
        WeatherForecastSelection secondSelection = second.Select(choices, "clear");

        Assert.Equal(firstSelection, secondSelection);
        uint checkpoint = first.State;
        WeatherForecastSelection expectedNext = first.Select(choices, "clear");

        var restored = new DeterministicWeatherForecast(1);
        restored.RestoreState(checkpoint);
        Assert.Equal(expectedNext, restored.Select(choices, "clear"));
    }
}
