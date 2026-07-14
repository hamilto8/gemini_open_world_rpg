using System;
using System.Collections.Generic;
using Xunit;
using Meridian.Core;
using Meridian.Data;
using Meridian.Dialogue;
using Meridian.NPC;
using Meridian.Quests;

namespace Meridian.Tests.NPC;

public class QuestDialogueTests
{
    [Fact]
    public void QuestManager_ShouldTrackProgressAndCompleteQuest()
    {
        var manager = new QuestManager();
        var quest = new BasicQuestDefinition
        {
            QuestId = "scrap_metal_quest",
            Objectives = new List<QuestObjective>
            {
                new("gather_scrap", ObjectiveType.GatherItem, "metal_scrap", 5),
            },
        };

        manager.RegisterQuest(quest);

        Assert.Equal(QuestState.NotStarted, manager.GetQuestState("scrap_metal_quest"));

        // Accept
        Assert.True(manager.AcceptQuest("scrap_metal_quest"));
        Assert.Equal(QuestState.Active, manager.GetQuestState("scrap_metal_quest"));
        Assert.Equal(0, manager.GetObjectiveProgress("scrap_metal_quest", "gather_scrap"));

        // Progress
        manager.IncrementObjective("metal_scrap", ObjectiveType.GatherItem, 3);
        Assert.Equal(3, manager.GetObjectiveProgress("scrap_metal_quest", "gather_scrap"));
        Assert.Equal(QuestState.Active, manager.GetQuestState("scrap_metal_quest"));

        // Complete
        manager.IncrementObjective("metal_scrap", ObjectiveType.GatherItem, 2);
        Assert.Equal(5, manager.GetObjectiveProgress("scrap_metal_quest", "gather_scrap"));
        Assert.Equal(QuestState.Completed, manager.GetQuestState("scrap_metal_quest"));
    }

    [Fact]
    public void DialogueService_ShouldNavigateBranchesAndExecuteSideEffects()
    {
        var dialogue = new DialogueService();
        bool questAccepted = false;

        var startNode = new DialogueNode("start", "QuestGiver", "Greetings traveler! Can you help me?");
        var acceptChoice = new DialogueChoice("Sure!", "accept_node", () => questAccepted = true);
        var declineChoice = new DialogueChoice("No thanks.", "end");
        startNode.Choices.Add(acceptChoice);
        startNode.Choices.Add(declineChoice);

        var acceptNode = new DialogueNode("accept_node", "QuestGiver", "Thank you so much!");
        acceptNode.Choices.Add(new DialogueChoice("Goodbye.", "end"));

        dialogue.RegisterNode(startNode);
        dialogue.RegisterNode(acceptNode);

        // Start dialog
        Assert.True(dialogue.StartDialogue("start"));
        Assert.Equal("QuestGiver", dialogue.CurrentNode?.Speaker);
        Assert.Equal("Greetings traveler! Can you help me?", dialogue.CurrentNode?.Text);

        // Accept (triggers side-effect and node transition)
        Assert.True(dialogue.SelectChoice(0));
        Assert.True(questAccepted);
        Assert.Equal("accept_node", dialogue.CurrentNode?.NodeId);

        // Goodbye
        Assert.True(dialogue.SelectChoice(0));
        Assert.Null(dialogue.CurrentNode);
    }

    [Fact]
    public void NpcScheduler_ShouldEvaluateStatesDependingOnClockHour()
    {
        var scheduler = new NpcScheduler();

        Assert.Equal(NpcActivityState.Sleeping, scheduler.EvaluateState(0));  // 12:00 AM -> Sleep
        Assert.Equal(NpcActivityState.Working, scheduler.EvaluateState(10)); // 10:00 AM -> Work
        Assert.Equal(NpcActivityState.Socializing, scheduler.EvaluateState(18)); // 06:00 PM -> Tavern
    }
}
