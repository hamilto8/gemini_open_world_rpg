namespace Meridian.Core.Logic;

// Concrete IGameAction implementations for the shared vocabulary (§3.6 item 2):
//   GiveItem, RemoveItem, GrantXp, SetWorldFlag, StartQuest, PlaySoundCue, ShowNotification,
//   TeleportPlayer, SpawnScene.
//
// Deferred (listed in §3.6 but not implemented — no supporting system exists yet; each is a new file
// + resource wrapper when its dependency lands, with zero edits to existing code):
//   - AdvanceQuest    : QuestManager has no "advance to next stage" API yet (§9.2).
//   - UnlockVehicle   : no vehicle registry / ownership model exists yet (§16.1 VehicleRegistry).
//   - StartDialogue   : the Dialogic DialogueBridge (§10.1) is not wired.
//   - GrantStatPoints : progression stat-point spending is not wired to a mutation entry point (§8.1).
//
// Every action is null/empty-argument tolerant: a meaningless argument is a no-op, never a throw.

/// <summary>Gives the player a quantity of an item.</summary>
public sealed class GiveItemAction : IGameAction
{
    private readonly string? _itemId;
    private readonly int _count;

    /// <summary>Creates a give-item action.</summary>
    public GiveItemAction(string itemId, int count)
    {
        _itemId = itemId;
        _count = count;
    }

    /// <inheritdoc />
    public void Execute(IActionContext context)
    {
        if (context is null || string.IsNullOrEmpty(_itemId) || _count <= 0)
        {
            return;
        }

        context.GiveItem(_itemId, _count);
    }
}

/// <summary>Removes a quantity of an item from the player.</summary>
public sealed class RemoveItemAction : IGameAction
{
    private readonly string? _itemId;
    private readonly int _count;

    /// <summary>Creates a remove-item action.</summary>
    public RemoveItemAction(string itemId, int count)
    {
        _itemId = itemId;
        _count = count;
    }

    /// <inheritdoc />
    public void Execute(IActionContext context)
    {
        if (context is null || string.IsNullOrEmpty(_itemId) || _count <= 0)
        {
            return;
        }

        context.RemoveItem(_itemId, _count);
    }
}

/// <summary>Grants experience points to the player.</summary>
public sealed class GrantXpAction : IGameAction
{
    private readonly int _amount;

    /// <summary>Creates a grant-XP action.</summary>
    public GrantXpAction(int amount) => _amount = amount;

    /// <inheritdoc />
    public void Execute(IActionContext context)
    {
        if (context is null || _amount <= 0)
        {
            return;
        }

        context.GrantXp(_amount);
    }
}

/// <summary>Sets a world flag to a boolean value.</summary>
public sealed class SetWorldFlagAction : IGameAction
{
    private readonly string? _flagId;
    private readonly bool _value;

    /// <summary>Creates a set-flag action.</summary>
    public SetWorldFlagAction(string flagId, bool value)
    {
        _flagId = flagId;
        _value = value;
    }

    /// <inheritdoc />
    public void Execute(IActionContext context)
    {
        if (context is null || string.IsNullOrEmpty(_flagId))
        {
            return;
        }

        context.SetWorldFlag(_flagId, _value);
    }
}

/// <summary>Starts (accepts) a quest by id.</summary>
public sealed class StartQuestAction : IGameAction
{
    private readonly string? _questId;

    /// <summary>Creates a start-quest action.</summary>
    public StartQuestAction(string questId) => _questId = questId;

    /// <inheritdoc />
    public void Execute(IActionContext context)
    {
        if (context is null || string.IsNullOrEmpty(_questId))
        {
            return;
        }

        context.StartQuest(_questId);
    }
}

/// <summary>Plays a sound cue by id.</summary>
public sealed class PlaySoundCueAction : IGameAction
{
    private readonly string? _cueId;

    /// <summary>Creates a play-cue action.</summary>
    public PlaySoundCueAction(string cueId) => _cueId = cueId;

    /// <inheritdoc />
    public void Execute(IActionContext context)
    {
        if (context is null || string.IsNullOrEmpty(_cueId))
        {
            return;
        }

        context.PlaySoundCue(_cueId);
    }
}

/// <summary>Shows a transient HUD notification.</summary>
public sealed class ShowNotificationAction : IGameAction
{
    private readonly string? _message;

    /// <summary>Creates a show-notification action.</summary>
    public ShowNotificationAction(string message) => _message = message;

    /// <inheritdoc />
    public void Execute(IActionContext context)
    {
        if (context is null || string.IsNullOrEmpty(_message))
        {
            return;
        }

        context.ShowNotification(_message);
    }
}

/// <summary>Teleports the player to a world position.</summary>
public sealed class TeleportPlayerAction : IGameAction
{
    private readonly float _x;
    private readonly float _y;
    private readonly float _z;

    /// <summary>Creates a teleport action.</summary>
    public TeleportPlayerAction(float x, float y, float z)
    {
        _x = x;
        _y = y;
        _z = z;
    }

    /// <inheritdoc />
    public void Execute(IActionContext context) => context?.TeleportPlayer(_x, _y, _z);
}

/// <summary>Spawns a scene at a world position.</summary>
public sealed class SpawnSceneAction : IGameAction
{
    private readonly string? _scenePath;
    private readonly float _x;
    private readonly float _y;
    private readonly float _z;

    /// <summary>Creates a spawn-scene action.</summary>
    public SpawnSceneAction(string scenePath, float x, float y, float z)
    {
        _scenePath = scenePath;
        _x = x;
        _y = y;
        _z = z;
    }

    /// <inheritdoc />
    public void Execute(IActionContext context)
    {
        if (context is null || string.IsNullOrEmpty(_scenePath))
        {
            return;
        }

        context.SpawnScene(_scenePath, _x, _y, _z);
    }
}
