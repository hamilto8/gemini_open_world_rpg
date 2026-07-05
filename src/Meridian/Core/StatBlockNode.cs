using Godot;
using System;

namespace Meridian.Core;

/// <summary>
/// Autoload or component Node wrapping the pure C# StatBlock. Exposes Godot signals.
/// </summary>
public partial class StatBlockNode : Node
{
    private readonly StatBlock _stats = new();

    public StatBlock Stats => _stats;

    [Signal]
    public delegate void StatChangedEventHandler(string statId, float newValue);

    public override void _Ready()
    {
        _stats.StatChanged += (statId, val) => EmitSignal(SignalName.StatChanged, statId, val);
    }

    public float GetStat(string statId) => _stats.GetStat(statId);
    public void SetBaseStat(string statId, float value) => _stats.SetBaseStat(statId, value);
    public void AddModifier(Modifier modifier) => _stats.AddModifier(modifier);
    public void RemoveModifier(Modifier modifier) => _stats.RemoveModifier(modifier);
    public void RemoveModifierBySource(string sourceTag) => _stats.RemoveModifierBySource(sourceTag);
    public void TickModifiers(double currentGameTimeMinutes) => _stats.TickModifiers(currentGameTimeMinutes);
}
