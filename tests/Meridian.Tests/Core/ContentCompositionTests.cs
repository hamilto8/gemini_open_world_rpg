using System;
using System.Collections.Generic;
using Meridian.Core.Logic;
using Meridian.Dialogue;
using Meridian.Factions;
using Meridian.NPC;
using Meridian.Quests;
using Meridian.Tests.Core.Logic;
using Xunit;

namespace Meridian.Tests.Core;

public class ContentCompositionTests
{
    [Fact]
    public void AuthoredDialogue_FiltersChoicesAndExecutesSharedActions()
    {
        var conditions = new FakeConditionContext();
        conditions.Flags["relay_open"] = true;
        var actions = new FakeActionContext();
        var service = new DialogueService(conditions, actions);
        service.RegisterDialogue(new TestDialogue(
            "dockmaster_intro",
            "start",
            new DialogueNodeDefinition("start", "Vale", "Need work?", new[]
            {
                new DialogueChoiceDefinition(
                    "Accept",
                    "end",
                    new ICondition[] { new WorldFlagCondition("relay_open", true) },
                    new IGameAction[] { new StartQuestAction("harbor_relay"), new SetWorldFlagAction("spoke_to_vale", true) }),
                new DialogueChoiceDefinition(
                    "Locked",
                    "end",
                    new ICondition[] { new WorldFlagCondition("never_set", true) },
                    Array.Empty<IGameAction>()),
            })));

        Assert.True(service.StartDialogue("dockmaster_intro"));
        Assert.Single(service.AvailableChoices);
        Assert.True(service.SelectChoice(0));
        Assert.Null(service.CurrentNode);
        Assert.Contains("harbor_relay", actions.QuestsStarted);
        Assert.True(actions.FlagsSet["spoke_to_vale"]);
    }

    [Fact]
    public void Quest_AcceptanceConditionsAndLifecycleActionsShareContexts()
    {
        var conditions = new FakeConditionContext();
        var actions = new FakeActionContext();
        var quests = new QuestManager(conditions, actions);
        quests.RegisterQuest(new TestQuest());

        Assert.False(quests.AcceptQuest("harbor_relay"));

        conditions.Flags["relay_open"] = true;
        Assert.True(quests.AcceptQuest("harbor_relay"));
        Assert.True(actions.FlagsSet["accepted"]);

        quests.IncrementObjective("metal_scrap", ObjectiveType.GatherItem, 3);
        Assert.Equal(QuestState.Completed, quests.GetQuestState("harbor_relay"));
        Assert.True(actions.FlagsSet["completed"]);
    }

    [Fact]
    public void FactionReputation_ClampsAndSupportsConditionVocabulary()
    {
        var factions = new FactionReputationService(new[]
        {
            new TestFaction("harbor_union", -10, 10, 0),
        });

        Assert.True(factions.ModifyReputation("harbor_union", 50));
        Assert.Equal(10, factions.GetReputation("harbor_union"));
        Assert.False(factions.ModifyReputation("unknown", 1));

        var restored = new FactionReputationService(new[] { new TestFaction("harbor_union", -10, 10, 0) });
        restored.RestoreState(factions.CaptureState());
        Assert.Equal(10, restored.GetReputation("harbor_union"));
    }

    [Theory]
    [InlineData(23, NpcActivityState.Sleeping)]
    [InlineData(5, NpcActivityState.Sleeping)]
    [InlineData(12, NpcActivityState.Working)]
    public void NpcScheduler_UsesAuthoredWraparoundSchedule(int hour, NpcActivityState expected)
    {
        var scheduler = new NpcScheduler(new[]
        {
            new NpcScheduleEntry(22, 6, NpcActivityState.Sleeping, 0, 0, 0),
            new NpcScheduleEntry(7, 21, NpcActivityState.Working, 1, 0, 1),
        });

        Assert.Equal(expected, scheduler.EvaluateState(hour));
    }

    private sealed record TestDialogue(
        string Id,
        string StartNodeId,
        params DialogueNodeDefinition[] NodeArray) : IDialogueDefinition
    {
        public IReadOnlyList<DialogueNodeDefinition> Nodes => NodeArray;
    }

    private sealed class TestQuest : IQuestDefinition
    {
        public string QuestId => "harbor_relay";
        public string DisplayName => "Harbor Relay";
        public string Description => "Test";
        public IReadOnlyList<QuestObjective> Objectives { get; } = new[]
        {
            new QuestObjective("collect_scrap", ObjectiveType.GatherItem, "metal_scrap", 3),
        };
        public IReadOnlyList<QuestReward> Rewards => Array.Empty<QuestReward>();
        public IReadOnlyList<ICondition> StartConditions { get; } = new ICondition[]
        {
            new WorldFlagCondition("relay_open", true),
        };
        public IReadOnlyList<IGameAction> OnAcceptActions { get; } = new IGameAction[]
        {
            new SetWorldFlagAction("accepted", true),
        };
        public IReadOnlyList<IGameAction> OnCompleteActions { get; } = new IGameAction[]
        {
            new SetWorldFlagAction("completed", true),
        };
    }

    private sealed record TestFaction(
        string Id,
        int MinimumReputation,
        int MaximumReputation,
        int StartingReputation) : IFactionDefinition
    {
        public string DisplayName => Id;
    }
}
