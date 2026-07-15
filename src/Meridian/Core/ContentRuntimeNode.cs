using System;
using System.Collections.Generic;
using Godot;
using Meridian.Core.Logic;
using Meridian.Core.Registry;
using Meridian.Core.Save;
using Meridian.Dialogue;
using Meridian.Environment;
using Meridian.Factions;
using Meridian.Quests;
using Meridian.Vehicles;
using Meridian.World;

namespace Meridian.Core;

/// <summary>
/// Composition root for authored RPG systems. It turns typed registry entries into persistent runtime
/// services and routes scheduled effects through the same condition/action vocabulary as dialogue and quests.
/// </summary>
public partial class ContentRuntimeNode : Node
{
    [Export] public string ProgressionProfileId { get; set; } = "default_progression";

    private IDisposable? _minuteSubscription;
    private QuestManager? _quests;
    private FastTravelNetwork? _fastTravel;
    private ScheduledEventRunner? _scheduledEvents;
    private FactionReputationService? _factions;
    private bool _composed;

    public override void _Ready()
    {
        if (!Services.TryGet<IContentDatabase>(out var database) || database is null)
        {
            GD.PushError("[ContentRuntime] IContentDatabase must be ready before ContentRuntimeNode.");
            return;
        }

        var conditions = new ServicesConditionContext(isInVehiclePredicate: static entity => entity is VehicleAvatar);
        var actions = new ServicesActionContext(
            warn: static message => GD.PushWarning($"[ContentRuntime] {message}"),
            teleport: TeleportPossessedEntity,
            spawnScene: SpawnScene);

        _factions = new FactionReputationService(Values(database.Factions));
        Services.Register<IFactionReputationService>(_factions);

        _quests = new QuestManager(conditions, actions);
        foreach (var definition in Values(database.Quests))
        {
            _quests.RegisterQuest(definition);
        }
        Services.Register<QuestManager>(_quests);

        var dialogue = new DialogueService(conditions, actions);
        foreach (var definition in Values(database.Dialogues))
        {
            dialogue.RegisterDialogue(definition);
        }
        Services.Register<DialogueService>(dialogue);

        _fastTravel = new FastTravelNetwork();
        foreach (var definition in Values(database.FastTravelPoints))
        {
            _fastTravel.RegisterNode(definition);
        }
        Services.Register<FastTravelNetwork>(_fastTravel);

        if (database.ProgressionProfiles.TryGet(ProgressionProfileId, out var progressionProfile)
            && progressionProfile is not null)
        {
            Services.Register<ProgressionManager>(new ProgressionManager(progressionProfile));
        }
        else
        {
            GD.PushWarning($"[ContentRuntime] Progression profile '{ProgressionProfileId}' is not registered.");
        }

        _scheduledEvents = new ScheduledEventRunner();
        foreach (var definition in Values(database.ScheduledEvents))
        {
            IScheduledEventDefinition captured = definition;
            Action callback = () => ExecuteScheduledEvent(captured, conditions, actions);
            if (definition.IsRecurring)
            {
                _scheduledEvents.RegisterDailyEvent(definition.Hour, definition.Minute, callback);
            }
            else
            {
                _scheduledEvents.RegisterOneShotEvent(definition.Hour, definition.Minute, callback);
            }
        }
        Services.Register<ScheduledEventRunner>(_scheduledEvents);

        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus is not null)
        {
            _minuteSubscription = eventBus.Subscribe<MinuteTickEvent>(OnMinuteTick);
        }

        if (Services.TryGet<ISaveService>(out var saveService) && saveService is not null)
        {
            saveService.RegisterParticipant(_quests);
            saveService.RegisterParticipant(_fastTravel);
            saveService.RegisterParticipant(_factions);
        }

        _composed = true;
        GD.Print($"[ContentRuntime] Composed {database.Quests.Count} quests, {database.Dialogues.Count} dialogues, "
            + $"{database.Npcs.Count} NPCs, {database.ScheduledEvents.Count} events, {database.Factions.Count} factions, "
            + $"and {database.FastTravelPoints.Count} fast-travel points.");
    }

    public override void _ExitTree()
    {
        if (!_composed)
        {
            return;
        }

        _minuteSubscription?.Dispose();
        _minuteSubscription = null;

        if (Services.TryGet<ISaveService>(out var saveService) && saveService is not null)
        {
            if (_quests is not null)
            {
                saveService.UnregisterParticipant(_quests);
            }
            if (_fastTravel is not null)
            {
                saveService.UnregisterParticipant(_fastTravel);
            }
            if (_factions is not null)
            {
                saveService.UnregisterParticipant(_factions);
            }
        }

        Services.Unregister<ScheduledEventRunner>();
        Services.Unregister<ProgressionManager>();
        Services.Unregister<FastTravelNetwork>();
        Services.Unregister<DialogueService>();
        Services.Unregister<QuestManager>();
        Services.Unregister<IFactionReputationService>();
    }

    private void OnMinuteTick(MinuteTickEvent tick) => _scheduledEvents?.Evaluate(tick.Hour, tick.Minute);

    private static void ExecuteScheduledEvent(
        IScheduledEventDefinition definition,
        IConditionContext conditions,
        IActionContext actions)
    {
        foreach (var condition in definition.Conditions)
        {
            if (condition is null || !condition.Evaluate(conditions))
            {
                return;
            }
        }

        foreach (var action in definition.Actions)
        {
            action?.Execute(actions);
        }
    }

    private static IEnumerable<T> Values<T>(IRegistry<T> registry) where T : class
    {
        foreach (var entry in registry.Entries)
        {
            yield return entry.Value;
        }
    }

    private void TeleportPossessedEntity(float x, float y, float z)
    {
        if (Services.TryGet<IPlayerController>(out var controller)
            && controller?.PossessedEntity is Node3D entity)
        {
            entity.GlobalPosition = new Vector3(x, y, z);
        }
    }

    private bool SpawnScene(string scenePath, float x, float y, float z)
    {
        PackedScene? packed = string.IsNullOrEmpty(scenePath) ? null : ResourceLoader.Load<PackedScene>(scenePath);
        if (packed is null)
        {
            return false;
        }

        Node instance = packed.Instantiate();
        AddChild(instance);
        if (instance is Node3D spatial)
        {
            spatial.GlobalPosition = new Vector3(x, y, z);
        }

        return true;
    }
}
