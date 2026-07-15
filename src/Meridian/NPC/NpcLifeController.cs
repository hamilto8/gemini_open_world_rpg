using Godot;
using System;
using Meridian.Core;
using Meridian.Environment;
using Meridian.Core.Registry;
using Meridian.Dialogue;
using Meridian.UI;

namespace Meridian.NPC;

/// <summary>
/// NPC Life controller that listens to clock triggers to move spatial nodes to target locations.
/// Delegates state evaluation to the pure C# NpcScheduler.
/// Enforces Section 15.0 and 15.2 requirements.
/// </summary>
public partial class NpcLifeController : Node3D, IInteractable
{
    [Export] public string NpcId { get; set; } = "";
    [Export] public Vector3 HomePosition { get; set; } = Vector3.Zero;
    [Export] public Vector3 WorkPosition { get; set; } = new Vector3(10, 0, 10);
    [Export] public Vector3 TavernPosition { get; set; } = new Vector3(-10, 0, 5);

    private NpcScheduler _scheduler = new();
    private NpcActivityState _currentState = NpcActivityState.Sleeping;
    private IDisposable? _hourSubscription;
    private string _displayName = "NPC";
    private string _dialogueId = "";

    public NpcActivityState CurrentState => _currentState;
    public string ObjectName => _displayName;
    public string ActionPrompt => TranslationServer.Translate("interaction.talk");

    public override void _Ready()
    {
        if (!string.IsNullOrEmpty(NpcId)
            && Services.TryGet<IContentDatabase>(out var database)
            && database is not null
            && database.Npcs.TryGet(NpcId, out var definition)
            && definition is not null)
        {
            _scheduler = new NpcScheduler(definition.Schedule);
            _displayName = definition.DisplayName;
            _dialogueId = definition.DialogueId;
        }

        UpdateForHour(Services.TryGet<IWorldClock>(out var clock) && clock is not null ? clock.CurrentHour : 0);

        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            // Store the token and dispose it in _ExitTree — this node is streamed in/out, so a
            // discarded token would leave a dangling handler on a freed Node (Section 3.3, non-negotiable).
            _hourSubscription = eventBus.Subscribe<HourChangedEvent>(OnHourChanged);
        }
    }

    public bool CanInteract(Node3D interactor)
        => !string.IsNullOrWhiteSpace(_dialogueId)
            && Services.TryGet<DialogueService>(out var dialogue)
            && dialogue != null;

    public void Interact(Node3D interactor)
    {
        if (!Services.TryGet<DialogueService>(out var dialogue)
            || dialogue == null
            || !dialogue.StartDialogue(_dialogueId))
        {
            GD.PushWarning($"[NpcLife] Unable to start dialogue '{_dialogueId}' for NPC '{NpcId}'.");
            return;
        }

        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new UIScreenRequestedEvent(UIScreenId.Dialogue));
        }
    }

    public override void _ExitTree()
    {
        _hourSubscription?.Dispose();
        _hourSubscription = null;
    }

    public void OnHourChanged(HourChangedEvent ev)
    {
        UpdateForHour(ev.Hour);
    }

    private void UpdateForHour(int hour)
    {
        NpcActivityState nextState = _scheduler.EvaluateState(hour);
        if (_currentState != nextState)
        {
            _currentState = nextState;
        }
        UpdateNpcPosition(hour);
    }

    private void UpdateNpcPosition(int hour)
    {
        if (_scheduler.TryEvaluateEntry(hour, out var entry))
        {
            GlobalPosition = new Vector3(entry.X, entry.Y, entry.Z);
            return;
        }

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
