using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Meridian.Core;

/// <summary>
/// StatBlock component attached to characters, vehicles, and items.
/// Manages base stats, caches modified values, and processes stat modifiers.
/// Enforces Section 3.6 item 3 requirements.
/// </summary>
public partial class StatBlock : Node
{
    private readonly Dictionary<string, float> _baseValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _cachedValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Modifier> _modifiers = new();
    private readonly HashSet<string> _dirtyStats = new(StringComparer.OrdinalIgnoreCase);

    [Signal]
    public delegate void StatChangedEventHandler(string statId, float newValue);

    public override void _Ready()
    {
        // Register default stats
        SetBaseStat("max_health", 100f);
        SetBaseStat("max_stamina", 100f);
        SetBaseStat("move_speed", 5.0f);
        SetBaseStat("armor", 0f);
        
        SetBaseStat("health", 100f);
        SetBaseStat("stamina", 100f);
    }

    public float GetStat(string statId)
    {
        ArgumentException.ThrowIfNullOrEmpty(statId);

        if (!_baseValues.ContainsKey(statId))
        {
            return 0f;
        }

        // If stat is dirty or not cached, recalculate
        if (_dirtyStats.Contains(statId) || !_cachedValues.ContainsKey(statId))
        {
            float baseVal = _baseValues[statId];
            var relevantModifiers = _modifiers.Where(m => m.TargetStatId.Equals(statId, StringComparison.OrdinalIgnoreCase));
            float finalVal = ModifierSystem.Calculate(baseVal, relevantModifiers);
            
            _cachedValues[statId] = finalVal;
            _dirtyStats.Remove(statId);
        }

        return _cachedValues[statId];
    }

    public void SetBaseStat(string statId, float value)
    {
        ArgumentException.ThrowIfNullOrEmpty(statId);

        _baseValues[statId] = value;
        _dirtyStats.Add(statId);
        
        EmitSignal(SignalName.StatChanged, statId, GetStat(statId));
    }

    public void AddModifier(Modifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);

        _modifiers.Add(modifier);
        _dirtyStats.Add(modifier.TargetStatId);

        EmitSignal(SignalName.StatChanged, modifier.TargetStatId, GetStat(modifier.TargetStatId));
    }

    public void RemoveModifier(string sourceTag)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceTag);

        var toRemove = _modifiers.Where(m => m.SourceTag.Equals(sourceTag, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var mod in toRemove)
        {
            _modifiers.Remove(mod);
            _dirtyStats.Add(mod.TargetStatId);
            EmitSignal(SignalName.StatChanged, mod.TargetStatId, GetStat(mod.TargetStatId));
        }
    }

    public void RemoveModifier(Modifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);

        if (_modifiers.Remove(modifier))
        {
            _dirtyStats.Add(modifier.TargetStatId);
            EmitSignal(SignalName.StatChanged, modifier.TargetStatId, GetStat(modifier.TargetStatId));
        }
    }

    /// <summary>
    /// Processes active modifiers, checking for duration-based expiries.
    /// Uses absolute game clock time where possible.
    /// </summary>
    public void TickModifiers(double currentGameTimeMinutes)
    {
        var expired = _modifiers.Where(m => m.ExpiryTime.HasValue && currentGameTimeMinutes >= m.ExpiryTime.Value).ToList();
        foreach (var mod in expired)
        {
            _modifiers.Remove(mod);
            _dirtyStats.Add(mod.TargetStatId);
            EmitSignal(SignalName.StatChanged, mod.TargetStatId, GetStat(mod.TargetStatId));
        }
    }
}
