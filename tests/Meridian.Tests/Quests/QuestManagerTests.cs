using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Meridian.Core.Save;
using Meridian.Quests;

namespace Meridian.Tests.Quests;

public class QuestManagerTests
{
    private static BasicQuestDefinition Quest(string id, params QuestObjective[] objectives) => new()
    {
        QuestId = id,
        Objectives = new List<QuestObjective>(objectives),
    };

    [Fact]
    public void MultiObjectiveQuest_CompletesOnlyWhenAllObjectivesMet()
    {
        var manager = new QuestManager();
        manager.RegisterQuest(Quest("q1",
            new QuestObjective("kill", ObjectiveType.KillTarget, "bandit", 2),
            new QuestObjective("gather", ObjectiveType.GatherItem, "herb", 1)));

        manager.AcceptQuest("q1");

        manager.IncrementObjective("bandit", ObjectiveType.KillTarget, 2);
        Assert.Equal(QuestState.Active, manager.GetQuestState("q1")); // gather still pending

        manager.IncrementObjective("herb", ObjectiveType.GatherItem, 1);
        Assert.Equal(QuestState.Completed, manager.GetQuestState("q1"));
    }

    [Fact]
    public void SecondQuestCompletingMidIncrement_DoesNotThrow()
    {
        // M5: both quests share the same objective target; incrementing it completes a quest
        // (mutating _questStates) while the increment loop is running. Snapshotting avoids the hazard.
        var manager = new QuestManager();
        manager.RegisterQuest(Quest("qa", new QuestObjective("o", ObjectiveType.GatherItem, "coin", 1)));
        manager.RegisterQuest(Quest("qb", new QuestObjective("o", ObjectiveType.GatherItem, "coin", 1)));
        manager.AcceptQuest("qa");
        manager.AcceptQuest("qb");

        manager.IncrementObjective("coin", ObjectiveType.GatherItem, 1);

        Assert.Equal(QuestState.Completed, manager.GetQuestState("qa"));
        Assert.Equal(QuestState.Completed, manager.GetQuestState("qb"));
    }

    [Fact]
    public void SaveRestore_ShouldRoundTripQuestProgress()
    {
        var manager = new QuestManager();
        manager.RegisterQuest(Quest("q1", new QuestObjective("gather", ObjectiveType.GatherItem, "scrap", 5)));
        manager.AcceptQuest("q1");
        manager.IncrementObjective("scrap", ObjectiveType.GatherItem, 3);

        var dto = manager.CaptureState();
        Assert.IsType<QuestSaveDto>(dto);

        var restored = new QuestManager();
        restored.RegisterQuest(Quest("q1", new QuestObjective("gather", ObjectiveType.GatherItem, "scrap", 5)));
        restored.RestoreState(dto);

        Assert.Equal(QuestState.Active, restored.GetQuestState("q1"));
        Assert.Equal(3, restored.GetObjectiveProgress("q1", "gather"));
    }

    [Fact]
    public void SaveRestore_ThroughSaveServiceJson_ShouldRoundTripNestedProgress()
    {
        // Verifies QuestSaveDto (with a nested Dictionary<string, Dictionary<string,int>>) survives
        // the real System.Text.Json source-generated pipeline, not just direct Capture/Restore.
        string dir = Path.Combine(Path.GetTempPath(), "MeridianQuestSave_" + Guid.NewGuid().ToString("N"));
        try
        {
            var save = new SaveService(dir);
            var qm = new QuestManager();
            qm.RegisterQuest(Quest("q1", new QuestObjective("gather", ObjectiveType.GatherItem, "scrap", 5)));
            qm.AcceptQuest("q1");
            qm.IncrementObjective("scrap", ObjectiveType.GatherItem, 3);
            save.RegisterParticipant(qm);
            save.SaveGame("slot");

            var save2 = new SaveService(dir);
            var qm2 = new QuestManager();
            qm2.RegisterQuest(Quest("q1", new QuestObjective("gather", ObjectiveType.GatherItem, "scrap", 5)));
            save2.RegisterParticipant(qm2);

            Assert.True(save2.LoadGame("slot"));
            Assert.Equal(QuestState.Active, qm2.GetQuestState("q1"));
            Assert.Equal(3, qm2.GetObjectiveProgress("q1", "gather"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
