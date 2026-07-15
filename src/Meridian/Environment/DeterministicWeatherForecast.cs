using System;
using System.Collections.Generic;

namespace Meridian.Environment;

public readonly record struct WeatherForecastChoice(
    string TargetWeatherId,
    float Weight,
    float Intensity,
    int MinDurationMinutes,
    int MaxDurationMinutes,
    float TransitionSeconds);

public readonly record struct WeatherForecastSelection(
    string WeatherId,
    float Intensity,
    int DurationMinutes,
    float TransitionSeconds);

/// <summary>Small deterministic PRNG/state selector so weather continues identically across save/load.</summary>
public sealed class DeterministicWeatherForecast
{
    private uint _state;

    public DeterministicWeatherForecast(uint seed)
    {
        _state = seed == 0 ? 0x6D2B79F5u : seed;
    }

    public uint State => _state;

    public void RestoreState(uint state) => _state = state == 0 ? 0x6D2B79F5u : state;

    public WeatherForecastSelection Select(IReadOnlyList<WeatherForecastChoice> choices, string fallbackWeatherId)
    {
        ArgumentNullException.ThrowIfNull(choices);
        float totalWeight = 0f;
        for (int i = 0; i < choices.Count; i++)
        {
            totalWeight += Math.Max(0f, choices[i].Weight);
        }

        if (choices.Count == 0 || totalWeight <= 0f)
        {
            return new WeatherForecastSelection(fallbackWeatherId, 1f, 180, 5f);
        }

        float cursor = NextUnit() * totalWeight;
        WeatherForecastChoice selected = choices[^1];
        for (int i = 0; i < choices.Count; i++)
        {
            selected = choices[i];
            cursor -= Math.Max(0f, selected.Weight);
            if (cursor <= 0f) break;
        }

        int min = Math.Max(1, selected.MinDurationMinutes);
        int max = Math.Max(min, selected.MaxDurationMinutes);
        int duration = min + (int)(NextUnit() * (max - min + 1));
        return new WeatherForecastSelection(
            selected.TargetWeatherId,
            Math.Clamp(selected.Intensity, 0f, 1f),
            Math.Min(duration, max),
            Math.Max(0f, selected.TransitionSeconds));
    }

    private float NextUnit()
    {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return (x & 0x00FFFFFFu) / 16777216f;
    }
}
