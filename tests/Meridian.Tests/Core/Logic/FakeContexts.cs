using System;
using System.Collections.Generic;
using Meridian.Core.Logic;

namespace Meridian.Tests.Core.Logic;

/// <summary>
/// Plain-C# fake implementing <see cref="IConditionContext"/> for headless condition tests.
/// Every accessor is backed by a settable field or dictionary.
/// </summary>
internal sealed class FakeConditionContext : IConditionContext
{
    public int Hour { get; set; }
    public string CurrentPhase { get; set; } = "Day";
    public string? CurrentWeatherId { get; set; }
    public bool IsInVehicle { get; set; }
    public string? CurrentRegionId { get; set; }

    public Dictionary<string, bool> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, float> Stats { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> ItemCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> QuestStates { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool GetWorldFlag(string id) => id is not null && Flags.TryGetValue(id, out var v) && v;

    public float GetStat(string id) => id is not null && Stats.TryGetValue(id, out var v) ? v : 0f;

    public int GetItemCount(string id) => id is not null && ItemCounts.TryGetValue(id, out var v) ? v : 0;

    public string? GetQuestState(string questId) =>
        questId is not null && QuestStates.TryGetValue(questId, out var v) ? v : null;
}

/// <summary>
/// Plain-C# fake implementing <see cref="IActionContext"/> for headless action/dispatcher tests.
/// Records every effect and lets tests force <see cref="GiveItem"/>/<see cref="RemoveItem"/>/
/// <see cref="SpawnScene"/> to refuse.
/// </summary>
internal sealed class FakeActionContext : IActionContext
{
    public bool GiveItemResult { get; set; } = true;
    public bool RemoveItemResult { get; set; } = true;
    public bool StartQuestResult { get; set; } = true;
    public bool SpawnSceneResult { get; set; } = true;

    public List<(string Id, int Count)> Given { get; } = new();
    public List<(string Id, int Count)> Removed { get; } = new();
    public int XpGranted { get; private set; }
    public Dictionary<string, bool> FlagsSet { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> QuestsStarted { get; } = new();
    public List<string> CuesPlayed { get; } = new();
    public List<string> Notifications { get; } = new();
    public List<(float X, float Y, float Z)> Teleports { get; } = new();
    public List<(string Path, float X, float Y, float Z)> Spawns { get; } = new();

    public bool GiveItem(string id, int count)
    {
        Given.Add((id, count));
        return GiveItemResult;
    }

    public bool RemoveItem(string id, int count)
    {
        Removed.Add((id, count));
        return RemoveItemResult;
    }

    public void GrantXp(int amount) => XpGranted += amount;

    public void SetWorldFlag(string id, bool value) => FlagsSet[id] = value;

    public bool StartQuest(string questId)
    {
        QuestsStarted.Add(questId);
        return StartQuestResult;
    }

    public void PlaySoundCue(string cueId) => CuesPlayed.Add(cueId);

    public void ShowNotification(string message) => Notifications.Add(message);

    public void TeleportPlayer(float x, float y, float z) => Teleports.Add((x, y, z));

    public bool SpawnScene(string scenePath, float x, float y, float z)
    {
        Spawns.Add((scenePath, x, y, z));
        return SpawnSceneResult;
    }
}
