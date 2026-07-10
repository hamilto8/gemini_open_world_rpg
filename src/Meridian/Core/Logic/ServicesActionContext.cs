using System;
using Meridian.Audio;
using Meridian.Items;
using Meridian.Quests;

namespace Meridian.Core.Logic;

/// <summary>
/// Production <see cref="IActionContext"/> that applies effects through services pulled from the
/// <see cref="Services"/> locator <em>lazily, per call</em>. A missing service is a safe no-op: void
/// actions do nothing, bool actions return false, and the miss is reported through the injectable
/// <c>warn</c> delegate rather than a Godot log call, so this type stays engine-free and testable
/// (§3.5, §10.2, §10.4).
/// </summary>
/// <remarks>
/// The two scene-bound effects — <see cref="TeleportPlayer"/> (needs the possessed Node3D's global
/// transform) and <see cref="SpawnScene"/> (needs the scene tree) — route through injectable delegates
/// with no-op / false defaults. The integration pass supplies real implementations from a Node.
/// </remarks>
public sealed class ServicesActionContext : IActionContext
{
    private readonly Action<string> _warn;
    private readonly Action<float, float, float> _teleport;
    private readonly Func<string, float, float, float, bool> _spawnScene;

    /// <summary>Creates a services-backed action context.</summary>
    /// <param name="warn">Receives a message when an effect is dropped because a service is missing.</param>
    /// <param name="teleport">Applies a player teleport; defaults to a no-op.</param>
    /// <param name="spawnScene">Spawns a scene, returning success; defaults to returning false.</param>
    public ServicesActionContext(
        Action<string>? warn = null,
        Action<float, float, float>? teleport = null,
        Func<string, float, float, float, bool>? spawnScene = null)
    {
        _warn = warn ?? (static _ => { });
        _teleport = teleport ?? (static (_, _, _) => { });
        _spawnScene = spawnScene ?? (static (_, _, _, _) => false);
    }

    /// <inheritdoc />
    public bool GiveItem(string id, int count)
    {
        if (string.IsNullOrEmpty(id) || count <= 0)
        {
            return false;
        }

        if (!Services.TryGet<IInventoryProvider>(out var provider) || provider is null)
        {
            _warn($"GiveItem('{id}', {count}) dropped: no IInventoryProvider registered.");
            return false;
        }

        return provider.Inventory.AddItem(new ItemInstance(id, count));
    }

    /// <inheritdoc />
    public bool RemoveItem(string id, int count)
    {
        if (string.IsNullOrEmpty(id) || count <= 0)
        {
            return false;
        }

        if (!Services.TryGet<IInventoryProvider>(out var provider) || provider is null)
        {
            _warn($"RemoveItem('{id}', {count}) dropped: no IInventoryProvider registered.");
            return false;
        }

        return provider.Inventory.RemoveItem(id, count);
    }

    /// <inheritdoc />
    public void GrantXp(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (Services.TryGet<ProgressionManager>(out var progression) && progression is not null)
        {
            progression.AddXp(amount);
        }
        else
        {
            _warn($"GrantXp({amount}) dropped: no ProgressionManager registered.");
        }
    }

    /// <inheritdoc />
    public void SetWorldFlag(string id, bool value)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        if (Services.TryGet<IWorldFlags>(out var flags) && flags is not null)
        {
            flags.SetFlag(id, value);
        }
        else
        {
            _warn($"SetWorldFlag('{id}', {value}) dropped: no IWorldFlags registered.");
        }
    }

    /// <inheritdoc />
    public bool StartQuest(string questId)
    {
        if (string.IsNullOrEmpty(questId))
        {
            return false;
        }

        if (!Services.TryGet<QuestManager>(out var quests) || quests is null)
        {
            _warn($"StartQuest('{questId}') dropped: no QuestManager registered.");
            return false;
        }

        return quests.AcceptQuest(questId);
    }

    /// <inheritdoc />
    public void PlaySoundCue(string cueId)
    {
        if (string.IsNullOrEmpty(cueId))
        {
            return;
        }

        if (Services.TryGet<IAudioDirector>(out var audio) && audio is not null)
        {
            audio.PlaySoundCue(cueId);
        }
        else
        {
            _warn($"PlaySoundCue('{cueId}') dropped: no IAudioDirector registered.");
        }
    }

    /// <inheritdoc />
    public void ShowNotification(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        if (Services.TryGet<IEventBus>(out var bus) && bus is not null)
        {
            bus.Publish(new HudNoticeEvent(message));
        }
        else
        {
            _warn($"ShowNotification('{message}') dropped: no IEventBus registered.");
        }
    }

    /// <inheritdoc />
    public void TeleportPlayer(float x, float y, float z) => _teleport(x, y, z);

    /// <inheritdoc />
    public bool SpawnScene(string scenePath, float x, float y, float z)
    {
        if (string.IsNullOrEmpty(scenePath))
        {
            return false;
        }

        return _spawnScene(scenePath, x, y, z);
    }
}
