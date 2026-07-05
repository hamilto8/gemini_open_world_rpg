using System;
using System.Collections.Generic;
using System.Linq;

namespace Meridian.Core;

/// <summary>
/// Pure C# StatBlock domain logic. Decoupled from Godot for headless testing.
/// Manages base stats, caches modified values, and processes active modifiers.
/// Enforces Section 3.6 item 3 and 3.2 layering guidelines.
/// </summary>
public class StatBlock
{
    private readonly Dictionary<string, float> _baseValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _cachedValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Modifier> _modifiers = new();
    private readonly HashSet<string> _dirtyStats = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string, float>? StatChanged;

    public StatBlock()
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
        
        StatChanged?.Invoke(statId, GetStat(statId));
    }

    public void AddModifier(Modifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);

        _modifiers.Add(modifier);
        _dirtyStats.Add(modifier.TargetStatId);

        StatChanged?.Invoke(modifier.TargetStatId, GetStat(modifier.TargetStatId));
    }

    public void RemoveModifier(Modifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);

        if (_modifiers.Remove(modifier))
        {
            _dirtyStats.Add(modifier.TargetStatId);
            StatChanged?.Invoke(modifier.TargetStatId, GetStat(modifier.TargetStatId));
        }
    }

    public void RemoveModifierBySource(string sourceTag)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceTag);

        var toRemove = _modifiers.Where(m => m.SourceTag.Equals(sourceTag, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var mod in toRemove)
        {
            _modifiers.Remove(mod);
            _dirtyStats.Add(mod.TargetStatId);
            StatChanged?.Invoke(mod.TargetStatId, GetStat(mod.TargetStatId));
        }
    }

    public void TickModifiers(double currentGameTimeMinutes)
    {
        var expired = _modifiers.Where(m => m.ExpiryTime.HasValue && currentGameTimeMinutes >= m.ExpiryTime.Value).ToList();
        foreach (var mod in expired)
        {
            _modifiers.Remove(mod);
            _dirtyStats.Add(mod.TargetStatId);
            StatChanged?.Invoke(mod.TargetStatId, GetStat(mod.TargetStatId));
        }
    }
}
