using Xunit;
using Meridian.Core.Logic;

namespace Meridian.Tests.Core.Logic;

public class ActionTests
{
    // --- GiveItemAction ---

    [Fact]
    public void GiveItem_LandsOnContext()
    {
        var ctx = new FakeActionContext();
        new GiveItemAction("medkit", 2).Execute(ctx);

        var given = Assert.Single(ctx.Given);
        Assert.Equal(("medkit", 2), given);
    }

    [Fact]
    public void GiveItem_RefusedByContext_DoesNotThrow()
    {
        // The context (e.g. a full inventory) may refuse; the action swallows the false result.
        var ctx = new FakeActionContext { GiveItemResult = false };
        new GiveItemAction("medkit", 2).Execute(ctx);
        Assert.Single(ctx.Given);
    }

    [Fact]
    public void GiveItem_NullEmptyOrNonPositive_IsNoOp()
    {
        var ctx = new FakeActionContext();
        new GiveItemAction(null!, 2).Execute(ctx);
        new GiveItemAction("", 2).Execute(ctx);
        new GiveItemAction("medkit", 0).Execute(ctx);
        new GiveItemAction("medkit", -3).Execute(ctx);
        new GiveItemAction("medkit", 1).Execute(null!);

        Assert.Empty(ctx.Given);
    }

    // --- RemoveItemAction ---

    [Fact]
    public void RemoveItem_LandsOnContext()
    {
        var ctx = new FakeActionContext();
        new RemoveItemAction("scrap", 3).Execute(ctx);

        var removed = Assert.Single(ctx.Removed);
        Assert.Equal(("scrap", 3), removed);
    }

    [Fact]
    public void RemoveItem_NullEmptyOrNonPositive_IsNoOp()
    {
        var ctx = new FakeActionContext();
        new RemoveItemAction(null!, 1).Execute(ctx);
        new RemoveItemAction("", 1).Execute(ctx);
        new RemoveItemAction("scrap", 0).Execute(ctx);
        new RemoveItemAction("scrap", 1).Execute(null!);

        Assert.Empty(ctx.Removed);
    }

    // --- GrantXpAction ---

    [Fact]
    public void GrantXp_LandsOnContext()
    {
        var ctx = new FakeActionContext();
        new GrantXpAction(150).Execute(ctx);
        Assert.Equal(150, ctx.XpGranted);
    }

    [Fact]
    public void GrantXp_NonPositiveAmount_IsNoOp()
    {
        var ctx = new FakeActionContext();
        new GrantXpAction(0).Execute(ctx);
        new GrantXpAction(-5).Execute(ctx);
        new GrantXpAction(10).Execute(null!);

        Assert.Equal(0, ctx.XpGranted);
    }

    // --- SetWorldFlagAction ---

    [Fact]
    public void SetWorldFlag_LandsOnContext()
    {
        var ctx = new FakeActionContext();
        new SetWorldFlagAction("betrayed_kel", true).Execute(ctx);
        new SetWorldFlagAction("met_marra", false).Execute(ctx);

        Assert.True(ctx.FlagsSet["betrayed_kel"]);
        Assert.False(ctx.FlagsSet["met_marra"]);
    }

    [Fact]
    public void SetWorldFlag_NullOrEmptyId_IsNoOp()
    {
        var ctx = new FakeActionContext();
        new SetWorldFlagAction(null!, true).Execute(ctx);
        new SetWorldFlagAction("", true).Execute(ctx);
        new SetWorldFlagAction("f", true).Execute(null!);

        Assert.Empty(ctx.FlagsSet);
    }

    // --- StartQuestAction ---

    [Fact]
    public void StartQuest_LandsOnContext()
    {
        var ctx = new FakeActionContext();
        new StartQuestAction("courier_gambit").Execute(ctx);
        Assert.Equal("courier_gambit", Assert.Single(ctx.QuestsStarted));
    }

    [Fact]
    public void StartQuest_RefusedByContext_DoesNotThrow()
    {
        var ctx = new FakeActionContext { StartQuestResult = false };
        new StartQuestAction("courier_gambit").Execute(ctx);
        Assert.Single(ctx.QuestsStarted);
    }

    [Fact]
    public void StartQuest_NullOrEmptyId_IsNoOp()
    {
        var ctx = new FakeActionContext();
        new StartQuestAction(null!).Execute(ctx);
        new StartQuestAction("").Execute(ctx);
        new StartQuestAction("q").Execute(null!);

        Assert.Empty(ctx.QuestsStarted);
    }

    // --- PlaySoundCueAction ---

    [Fact]
    public void PlaySoundCue_LandsOnContext()
    {
        var ctx = new FakeActionContext();
        new PlaySoundCueAction("ui_quest_complete").Execute(ctx);
        Assert.Equal("ui_quest_complete", Assert.Single(ctx.CuesPlayed));
    }

    [Fact]
    public void PlaySoundCue_NullOrEmptyId_IsNoOp()
    {
        var ctx = new FakeActionContext();
        new PlaySoundCueAction(null!).Execute(ctx);
        new PlaySoundCueAction("").Execute(ctx);
        new PlaySoundCueAction("cue").Execute(null!);

        Assert.Empty(ctx.CuesPlayed);
    }

    // --- ShowNotificationAction ---

    [Fact]
    public void ShowNotification_LandsOnContext()
    {
        var ctx = new FakeActionContext();
        new ShowNotificationAction("Quest updated").Execute(ctx);
        Assert.Equal("Quest updated", Assert.Single(ctx.Notifications));
    }

    [Fact]
    public void ShowNotification_NullOrEmptyMessage_IsNoOp()
    {
        var ctx = new FakeActionContext();
        new ShowNotificationAction(null!).Execute(ctx);
        new ShowNotificationAction("").Execute(ctx);
        new ShowNotificationAction("hi").Execute(null!);

        Assert.Empty(ctx.Notifications);
    }

    // --- TeleportPlayerAction ---

    [Fact]
    public void TeleportPlayer_LandsOnContext()
    {
        var ctx = new FakeActionContext();
        new TeleportPlayerAction(1.5f, -2f, 30f).Execute(ctx);

        var teleport = Assert.Single(ctx.Teleports);
        Assert.Equal((1.5f, -2f, 30f), teleport);
    }

    [Fact]
    public void TeleportPlayer_NullContext_DoesNotThrow()
    {
        new TeleportPlayerAction(0f, 0f, 0f).Execute(null!);
    }

    // --- SpawnSceneAction ---

    [Fact]
    public void SpawnScene_LandsOnContext()
    {
        var ctx = new FakeActionContext();
        new SpawnSceneAction("res://scenes/fx/explosion.tscn", 1f, 2f, 3f).Execute(ctx);

        var spawn = Assert.Single(ctx.Spawns);
        Assert.Equal(("res://scenes/fx/explosion.tscn", 1f, 2f, 3f), spawn);
    }

    [Fact]
    public void SpawnScene_RefusedByContext_DoesNotThrow()
    {
        var ctx = new FakeActionContext { SpawnSceneResult = false };
        new SpawnSceneAction("res://x.tscn", 0f, 0f, 0f).Execute(ctx);
        Assert.Single(ctx.Spawns);
    }

    [Fact]
    public void SpawnScene_NullOrEmptyPath_IsNoOp()
    {
        var ctx = new FakeActionContext();
        new SpawnSceneAction(null!, 0f, 0f, 0f).Execute(ctx);
        new SpawnSceneAction("", 0f, 0f, 0f).Execute(ctx);
        new SpawnSceneAction("res://x.tscn", 0f, 0f, 0f).Execute(null!);

        Assert.Empty(ctx.Spawns);
    }
}
