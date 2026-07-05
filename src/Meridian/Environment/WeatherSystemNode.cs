using Godot;
using System;
using Meridian.Core;
using Meridian.Data;

namespace Meridian.Environment;

/// <summary>
/// Autoload Node managing weather transitions, ambient environment interpolation, and stat modifier pushes.
/// Enforces Section 11.4 and 11.5 requirements.
/// </summary>
public partial class WeatherSystemNode : Node
{
    [Export] public WeatherProfile? DefaultWeather { get; set; }

    private WeatherProfile? _currentWeather;
    private Modifier? _activeWeatherModifier;

    public WeatherProfile? CurrentWeather => _currentWeather;

    public override void _Ready()
    {
        _currentWeather = DefaultWeather;
        
        // Subscribe to minute ticks to simulate scheduler trigger checks if needed
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Subscribe<MinuteTickEvent>(OnMinuteTick);
        }
    }

    public void TransitionTo(WeatherProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (_currentWeather?.WeatherId == profile.WeatherId) return;

        GD.Print($"[WeatherSystem] Transitioning from '{_currentWeather?.WeatherId}' to '{profile.WeatherId}'");

        // Remove old modifier if active
        RemoveWeatherModifiers();

        _currentWeather = profile;

        // Apply new modifiers (Section 11.5 weather modifier push)
        ApplyWeatherModifiers();

        // Publish event to EventBus
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new WeatherTransitionEvent(profile.WeatherId));
        }
    }

    private void ApplyWeatherModifiers()
    {
        if (_currentWeather == null || _currentWeather.MoveSpeedModifier == 0f) return;

        // Find player controller and possessed avatar's StatBlockNode
        if (Services.TryGet<IPlayerController>(out var pc) && pc?.PossessedEntity is Node avatarNode)
        {
            var stats = avatarNode.GetNodeOrNull<StatBlockNode>("StatBlock");
            if (stats != null)
            {
                _activeWeatherModifier = new Modifier(
                    targetStatId: "move_speed",
                    operation: ModifierOp.Add,
                    value: _currentWeather.MoveSpeedModifier,
                    sourceTag: $"weather_{_currentWeather.WeatherId}"
                );
                
                stats.AddModifier(_activeWeatherModifier);
                GD.Print($"[WeatherSystem] Applied move_speed modifier ({_currentWeather.MoveSpeedModifier}) to player.");
            }
        }
    }

    private void RemoveWeatherModifiers()
    {
        if (_activeWeatherModifier == null) return;

        if (Services.TryGet<IPlayerController>(out var pc) && pc?.PossessedEntity is Node avatarNode)
        {
            var stats = avatarNode.GetNodeOrNull<StatBlockNode>("StatBlock");
            if (stats != null)
            {
                stats.RemoveModifier(_activeWeatherModifier);
                GD.Print("[WeatherSystem] Cleared active weather modifiers.");
            }
        }

        _activeWeatherModifier = null;
    }

    private void OnMinuteTick(MinuteTickEvent tick)
    {
        // Simple ambient tick check
    }
}

public record struct WeatherTransitionEvent(string WeatherId);
