using Godot;
using System;
using Meridian.Environment;

namespace Meridian.Core;

/// <summary>
/// Autoload or component Node wrapping the pure C# StatBlock. Exposes Godot signals.
/// </summary>
public partial class StatBlockNode : Node
{
    private readonly StatBlock _stats = new();
    private IDisposable? _minuteSubscription;

    public StatBlock Stats => _stats;

    [Signal]
    public delegate void StatChangedEventHandler(string statId, float newValue);

    public override void _Ready()
    {
        _stats.StatChanged += (statId, val) => EmitSignal(SignalName.StatChanged, statId, val);
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            _minuteSubscription = eventBus.Subscribe<MinuteTickEvent>(OnMinuteTick);
        }
    }

    public override void _ExitTree()
    {
        _minuteSubscription?.Dispose();
        _minuteSubscription = null;
    }

    private void OnMinuteTick(MinuteTickEvent _)
    {
        if (Services.TryGet<IWorldClock>(out var clock) && clock != null)
        {
            _stats.TickModifiers(clock.TotalGameMinutes);
        }
    }

    public float GetStat(string statId) => _stats.GetStat(statId);
    public void SetBaseStat(string statId, float value) => _stats.SetBaseStat(statId, value);
    public void AddModifier(Modifier modifier) => _stats.AddModifier(modifier);
    public void RemoveModifier(Modifier modifier) => _stats.RemoveModifier(modifier);
    public void RemoveModifierBySource(string sourceTag) => _stats.RemoveModifierBySource(sourceTag);
    public void TickModifiers(double currentGameTimeMinutes) => _stats.TickModifiers(currentGameTimeMinutes);
}
