using Godot;
using Meridian.Core.Logic;

namespace Meridian.Data;

/// <summary>
/// Abstract editor-facing wrapper for the shared action vocabulary (§3.6 item 2). Each concrete subclass
/// is a dumb data container: it exposes <c>[Export]</c> fields and maps them onto an engine-free
/// <see cref="IGameAction"/> via <see cref="ToAction"/>. All effect logic lives in
/// <c>Meridian.Core.Logic</c> so it can be unit-tested headlessly; these wrappers are not tested
/// (ADR-0003).
/// </summary>
/// <remarks>
/// Deferred from the §3.6 list (no supporting system exists yet — each becomes one new subclass + one
/// new domain action when its dependency lands): <c>AdvanceQuest</c> (no quest-advance API, §9.2),
/// <c>UnlockVehicle</c> (no vehicle registry, §16.1), <c>StartDialogue</c> (no DialogueBridge, §10.1),
/// <c>GrantStatPoints</c> (no stat-point mutation entry point, §8.1).
/// </remarks>
public abstract partial class GameActionResource : Resource
{
    /// <summary>Maps this resource's exported data onto an engine-free action.</summary>
    public abstract IGameAction ToAction();
}

/// <summary>Wrapper for <see cref="GiveItemAction"/>.</summary>
[GlobalClass]
public partial class GiveItemActionResource : GameActionResource
{
    /// <summary>Item id to grant.</summary>
    [Export] public string ItemId { get; set; } = "";

    /// <summary>Quantity to grant.</summary>
    [Export] public int Count { get; set; } = 1;

    /// <inheritdoc />
    public override IGameAction ToAction() => new GiveItemAction(ItemId, Count);
}

/// <summary>Wrapper for <see cref="RemoveItemAction"/>.</summary>
[GlobalClass]
public partial class RemoveItemActionResource : GameActionResource
{
    /// <summary>Item id to remove.</summary>
    [Export] public string ItemId { get; set; } = "";

    /// <summary>Quantity to remove.</summary>
    [Export] public int Count { get; set; } = 1;

    /// <inheritdoc />
    public override IGameAction ToAction() => new RemoveItemAction(ItemId, Count);
}

/// <summary>Wrapper for <see cref="GrantXpAction"/>.</summary>
[GlobalClass]
public partial class GrantXpActionResource : GameActionResource
{
    /// <summary>XP amount to grant.</summary>
    [Export] public int Amount { get; set; } = 1;

    /// <inheritdoc />
    public override IGameAction ToAction() => new GrantXpAction(Amount);
}

/// <summary>Wrapper for <see cref="SetWorldFlagAction"/>.</summary>
[GlobalClass]
public partial class SetWorldFlagActionResource : GameActionResource
{
    /// <summary>Flag id to set.</summary>
    [Export] public string FlagId { get; set; } = "";

    /// <summary>Value to store.</summary>
    [Export] public bool Value { get; set; } = true;

    /// <inheritdoc />
    public override IGameAction ToAction() => new SetWorldFlagAction(FlagId, Value);
}

/// <summary>Wrapper for <see cref="StartQuestAction"/>.</summary>
[GlobalClass]
public partial class StartQuestActionResource : GameActionResource
{
    /// <summary>Quest id to start.</summary>
    [Export] public string QuestId { get; set; } = "";

    /// <inheritdoc />
    public override IGameAction ToAction() => new StartQuestAction(QuestId);
}

/// <summary>Wrapper for <see cref="PlaySoundCueAction"/>.</summary>
[GlobalClass]
public partial class PlaySoundCueActionResource : GameActionResource
{
    /// <summary>Sound cue id to play.</summary>
    [Export] public string CueId { get; set; } = "";

    /// <inheritdoc />
    public override IGameAction ToAction() => new PlaySoundCueAction(CueId);
}

/// <summary>Wrapper for <see cref="ShowNotificationAction"/>.</summary>
[GlobalClass]
public partial class ShowNotificationActionResource : GameActionResource
{
    /// <summary>Message to display.</summary>
    [Export(PropertyHint.MultilineText)] public string Message { get; set; } = "";

    /// <inheritdoc />
    public override IGameAction ToAction() => new ShowNotificationAction(Message);
}

/// <summary>Wrapper for <see cref="TeleportPlayerAction"/>. Exposes a Vector3, mapped to primitive floats.</summary>
[GlobalClass]
public partial class TeleportPlayerActionResource : GameActionResource
{
    /// <summary>Target world position.</summary>
    [Export] public Vector3 Position { get; set; }

    /// <inheritdoc />
    public override IGameAction ToAction() => new TeleportPlayerAction(Position.X, Position.Y, Position.Z);
}

/// <summary>Wrapper for <see cref="SpawnSceneAction"/>. Exposes a Vector3, mapped to primitive floats.</summary>
[GlobalClass]
public partial class SpawnSceneActionResource : GameActionResource
{
    /// <summary>Path to the scene to spawn (e.g. "res://scenes/...").</summary>
    [Export] public string ScenePath { get; set; } = "";

    /// <summary>Target world position.</summary>
    [Export] public Vector3 Position { get; set; }

    /// <inheritdoc />
    public override IGameAction ToAction() => new SpawnSceneAction(ScenePath, Position.X, Position.Y, Position.Z);
}
