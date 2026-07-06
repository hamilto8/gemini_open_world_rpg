using Godot;
using System;
using Meridian.Core;
using Meridian.Environment;

namespace Meridian.NPC;

/// <summary>
/// NPC Life controller that listens to clock triggers to move spatial nodes to target locations.
/// Delegates state evaluation to the pure C# NpcScheduler.
/// Enforces Section 15.0 and 15.2 requirements.
/// </summary>
public partial class NpcLifeController : Node3D
{
    [Export] public Vector3 HomePosition { get; set; } = Vector3.Zero;
    [Export] public Vector3 WorkPosition { get; set; } = new Vector3(10, 0, 10);
    [Export] public Vector3 TavernPosition { get; set; } = new Vector3(-10, 0, 5);

    private readonly NpcScheduler _scheduler = new();
    private NpcActivityState _currentState = NpcActivityState.Sleeping;

    public NpcActivityState CurrentState => _currentState;

    public override void _Ready()
    {
        // Set initial position
        GlobalPosition = HomePosition;

        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Subscribe<HourChangedEvent>(OnHourChanged);
        }
    }

    public void OnHourChanged(HourChangedEvent ev)
    {
        NpcActivityState nextState = _scheduler.EvaluateState(ev.Hour);
        if (_currentState != nextState)
        {
            _currentState = nextState;
            UpdateNpcPosition();
        }
    }

    private void UpdateNpcPosition()
    {
        switch (_currentState)
        {
            case NpcActivityState.Working:
                GlobalPosition = WorkPosition;
                GD.Print($"[NpcLife] {GetParent().Name} moving to WORK position {WorkPosition}");
                break;
            case NpcActivityState.Socializing:
                GlobalPosition = TavernPosition;
                GD.Print($"[NpcLife] {GetParent().Name} moving to TAVERN position {TavernPosition}");
                break;
            case NpcActivityState.Sleeping:
            default:
                GlobalPosition = HomePosition;
                GD.Print($"[NpcLife] {GetParent().Name} moving to HOME position {HomePosition}");
                break;
        }
    }
}
