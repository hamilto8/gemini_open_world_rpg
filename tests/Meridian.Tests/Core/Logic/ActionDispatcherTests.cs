using System;
using System.Linq;
using Xunit;
using Meridian.Core.Logic;

namespace Meridian.Tests.Core.Logic;

public class ActionDispatcherTests
{
    private readonly ActionDispatcher _dispatcher = new();
    private readonly FakeActionContext _context = new();

    private bool Dispatch(string verb, out string error, params string[] args) =>
        _dispatcher.TryDispatch(verb, args, _context, out error);

    // --- Happy path per verb ---

    [Fact]
    public void GiveItem_HappyPath()
    {
        Assert.True(Dispatch("give_item", out string error, "medkit", "2"));
        Assert.Equal(string.Empty, error);
        Assert.Equal(("medkit", 2), Assert.Single(_context.Given));
    }

    [Fact]
    public void RemoveItem_HappyPath()
    {
        Assert.True(Dispatch("remove_item", out _, "scrap", "3"));
        Assert.Equal(("scrap", 3), Assert.Single(_context.Removed));
    }

    [Fact]
    public void GrantXp_HappyPath()
    {
        Assert.True(Dispatch("grant_xp", out _, "250"));
        Assert.Equal(250, _context.XpGranted);
    }

    [Fact]
    public void SetFlag_HappyPath_TrueAndFalse()
    {
        Assert.True(Dispatch("set_flag", out _, "betrayed_kel", "true"));
        Assert.True(Dispatch("set_flag", out _, "met_marra", "false"));

        Assert.True(_context.FlagsSet["betrayed_kel"]);
        Assert.False(_context.FlagsSet["met_marra"]);
    }

    [Fact]
    public void PlayCue_HappyPath()
    {
        Assert.True(Dispatch("play_cue", out _, "ui_quest_complete"));
        Assert.Equal("ui_quest_complete", Assert.Single(_context.CuesPlayed));
    }

    [Fact]
    public void Notify_JoinsRemainingArgsIntoOneMessage()
    {
        Assert.True(Dispatch("notify", out _, "Quest", "updated:", "Storm", "Chaser"));
        Assert.Equal("Quest updated: Storm Chaser", Assert.Single(_context.Notifications));
    }

    [Fact]
    public void TeleportPlayer_HappyPath_ParsesFloats()
    {
        Assert.True(Dispatch("teleport_player", out _, "1.5", "-2", "30.25"));
        Assert.Equal((1.5f, -2f, 30.25f), Assert.Single(_context.Teleports));
    }

    [Fact]
    public void StartQuest_HappyPath()
    {
        Assert.True(Dispatch("start_quest", out _, "courier_gambit"));
        Assert.Equal("courier_gambit", Assert.Single(_context.QuestsStarted));
    }

    [Fact]
    public void SpawnScene_HappyPath()
    {
        Assert.True(Dispatch("spawn_scene", out _, "res://scenes/fx/explosion.tscn", "1", "2", "3"));
        Assert.Equal(("res://scenes/fx/explosion.tscn", 1f, 2f, 3f), Assert.Single(_context.Spawns));
    }

    [Fact]
    public void VerbLookup_IsCaseInsensitive()
    {
        Assert.True(Dispatch("GIVE_ITEM", out _, "medkit", "1"));
        Assert.Single(_context.Given);
    }

    // --- Unknown verb ---

    [Fact]
    public void UnknownVerb_FailsWithActionableError()
    {
        Assert.False(Dispatch("rep_delta", out string error, "harbor_guild", "+10"));
        Assert.Contains("unknown action verb 'rep_delta'", error);
        Assert.Contains("give_item", error); // error lists the known verbs
    }

    [Fact]
    public void NullOrWhitespaceVerb_FailsWithoutThrowing()
    {
        Assert.False(_dispatcher.TryDispatch(null!, Array.Empty<string>(), _context, out string error));
        Assert.Contains("no action verb", error);

        Assert.False(Dispatch("   ", out error));
        Assert.Contains("no action verb", error);
    }

    [Fact]
    public void NullContext_FailsWithoutThrowing()
    {
        Assert.False(_dispatcher.TryDispatch("grant_xp", new[] { "10" }, null!, out string error));
        Assert.Contains("no action context", error);
    }

    [Fact]
    public void NullArgs_TreatedAsEmpty_FailsArityNotThrow()
    {
        Assert.False(_dispatcher.TryDispatch("grant_xp", null!, _context, out string error));
        Assert.Contains("expected 1 argument", error);
    }

    // --- Wrong arity ---

    [Theory]
    [InlineData("give_item", new[] { "medkit" }, "expected 2 arguments but got 1")]
    [InlineData("give_item", new[] { "medkit", "2", "extra" }, "expected 2 arguments but got 3")]
    [InlineData("grant_xp", new string[0], "expected 1 argument but got 0")]
    [InlineData("teleport_player", new[] { "1", "2" }, "expected 3 arguments but got 2")]
    [InlineData("spawn_scene", new[] { "res://x.tscn", "1", "2" }, "expected 4 arguments but got 3")]
    public void WrongArity_FailsWithCountAndUsage(string verb, string[] args, string expectedFragment)
    {
        Assert.False(_dispatcher.TryDispatch(verb, args, _context, out string error));
        Assert.Contains(expectedFragment, error);
        Assert.Contains("Usage:", error);
    }

    // --- Unparseable numeric args ---

    [Fact]
    public void GiveItem_UnparseableCount_FailsNamingTheParameter()
    {
        Assert.False(Dispatch("give_item", out string error, "medkit", "lots"));
        Assert.Contains("<count>", error);
        Assert.Contains("'lots'", error);
        Assert.Contains("integer", error);
        Assert.Empty(_context.Given); // nothing executed
    }

    [Fact]
    public void GrantXp_UnparseableAmount_Fails()
    {
        Assert.False(Dispatch("grant_xp", out string error, "much"));
        Assert.Contains("<amount>", error);
        Assert.Equal(0, _context.XpGranted);
    }

    [Fact]
    public void TeleportPlayer_UnparseableCoordinate_FailsNamingTheAxis()
    {
        Assert.False(Dispatch("teleport_player", out string error, "1", "up", "3"));
        Assert.Contains("<y>", error);
        Assert.Contains("'up'", error);
        Assert.Empty(_context.Teleports);
    }

    [Fact]
    public void SpawnScene_UnparseableCoordinate_Fails()
    {
        Assert.False(Dispatch("spawn_scene", out string error, "res://x.tscn", "a", "0", "0"));
        Assert.Contains("<x>", error);
        Assert.Empty(_context.Spawns);
    }

    [Fact]
    public void SetFlag_UnparseableBool_Fails()
    {
        Assert.False(Dispatch("set_flag", out string error, "some_flag", "maybe"));
        Assert.Contains("'maybe'", error);
        Assert.Contains("true|false", error);
        Assert.Empty(_context.FlagsSet);
    }

    // --- Enumeration for the validator / console help ---

    [Fact]
    public void RegisteredVerbs_ListsEveryStandardVerb()
    {
        var verbs = _dispatcher.RegisteredVerbs.ToList();

        string[] expected =
        {
            "give_item", "remove_item", "grant_xp", "set_flag", "play_cue",
            "notify", "teleport_player", "start_quest", "spawn_scene"
        };

        Assert.Equal(expected.Length, verbs.Count);
        foreach (string verb in expected)
        {
            Assert.Contains(verb, verbs);
        }
    }

    [Fact]
    public void UsageLines_ProvideOnePerVerb()
    {
        var usages = _dispatcher.UsageLines.ToList();
        Assert.Equal(_dispatcher.RegisteredVerbs.Count(), usages.Count);
        Assert.Contains("give_item <id> <count>", usages);
        Assert.Contains("notify <message...>", usages);
    }

    [Fact]
    public void GetUsage_KnownAndUnknownVerbs()
    {
        Assert.Equal("set_flag <id> <true|false>", _dispatcher.GetUsage("set_flag"));
        Assert.Null(_dispatcher.GetUsage("rep_delta"));
    }
}
