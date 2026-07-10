using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using Meridian.Core;
using Meridian.Core.Save;

namespace Meridian.Tests.Core.Logic;

public class WorldFlagsTests
{
    [Fact]
    public void GetFlag_AbsentFlag_DefaultsToFalse()
    {
        var flags = new WorldFlagsService();
        Assert.False(flags.GetFlag("never_set"));
        Assert.False(flags.GetFlag(""));
        Assert.False(flags.GetFlag(null!));
    }

    [Fact]
    public void SetFlag_ThenGetFlag_RoundTrips()
    {
        var flags = new WorldFlagsService();

        flags.SetFlag("betrayed_kel", true);
        Assert.True(flags.GetFlag("betrayed_kel"));

        flags.SetFlag("betrayed_kel", false);
        Assert.False(flags.GetFlag("betrayed_kel"));
    }

    [Fact]
    public void FlagIds_AreCaseInsensitive()
    {
        var flags = new WorldFlagsService();
        flags.SetFlag("Met_Marra", true);
        Assert.True(flags.GetFlag("met_marra"));
    }

    [Fact]
    public void StringValues_RoundTripThroughTryGetValue()
    {
        var flags = new WorldFlagsService();

        Assert.False(flags.TryGetValue("chosen_faction", out _));

        flags.SetValue("chosen_faction", "harbor_guild");
        Assert.True(flags.TryGetValue("chosen_faction", out string value));
        Assert.Equal("harbor_guild", value);

        // A non-boolean string value reads false through the boolean accessor.
        Assert.False(flags.GetFlag("chosen_faction"));
    }

    [Fact]
    public void FlagChanged_FiresOnChange_NotOnIdempotentWrite()
    {
        var flags = new WorldFlagsService();
        var changed = new List<string>();
        flags.FlagChanged += id => changed.Add(id);

        flags.SetFlag("storm_chaser_done", true);
        flags.SetFlag("storm_chaser_done", true); // no-op: same value
        flags.SetFlag("storm_chaser_done", false);

        Assert.Equal(new[] { "storm_chaser_done", "storm_chaser_done" }, changed);
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        var flags = new WorldFlagsService();
        flags.SetFlag("a", true);
        flags.SetValue("b", "x");

        flags.Clear();

        Assert.False(flags.GetFlag("a"));
        Assert.False(flags.TryGetValue("b", out _));
    }

    [Fact]
    public void SaveParticipant_ContractMatchesReservedSlot()
    {
        var flags = new WorldFlagsService();
        Assert.Equal("WorldFlags", flags.ParticipantId);
        Assert.Equal(10, flags.RestoreOrder); // reserved for flags per ISaveParticipant doc (§16.2)
        Assert.Equal(typeof(WorldFlagsDto), flags.StateType);
    }

    [Fact]
    public void CaptureRestore_RoundTripsThroughDto()
    {
        var source = new WorldFlagsService();
        source.SetFlag("betrayed_kel", true);
        source.SetFlag("met_marra", false);
        source.SetValue("chosen_faction", "harbor_guild");

        var dto = Assert.IsType<WorldFlagsDto>(source.CaptureState());

        var restored = new WorldFlagsService();
        restored.RestoreState(dto);

        Assert.True(restored.GetFlag("betrayed_kel"));
        Assert.False(restored.GetFlag("met_marra"));
        Assert.True(restored.TryGetValue("met_marra", out _)); // explicitly-false flag is still present
        Assert.True(restored.TryGetValue("chosen_faction", out string faction));
        Assert.Equal("harbor_guild", faction);
    }

    [Fact]
    public void CapturedDto_IsSnapshot_NotLiveView()
    {
        var flags = new WorldFlagsService();
        flags.SetFlag("a", true);

        var dto = Assert.IsType<WorldFlagsDto>(flags.CaptureState());
        flags.SetFlag("a", false); // mutate after capture

        Assert.Equal("true", dto.Flags["a"]);
    }

    [Fact]
    public void WorldFlagsDto_RoundTripsThroughSaveJsonContext()
    {
        // Serialize with the source-generated context exactly as SaveService does (§16.2).
        var source = new WorldFlagsService();
        source.SetFlag("betrayed_kel", true);
        source.SetValue("chosen_faction", "harbor_guild");

        object dto = source.CaptureState();
        string json = JsonSerializer.Serialize(dto, source.StateType, SaveJsonContext.Default);

        var rehydrated = JsonSerializer.Deserialize(json, source.StateType, SaveJsonContext.Default);
        Assert.NotNull(rehydrated);

        var restored = new WorldFlagsService();
        restored.RestoreState(rehydrated!);

        Assert.True(restored.GetFlag("betrayed_kel"));
        Assert.True(restored.TryGetValue("chosen_faction", out string faction));
        Assert.Equal("harbor_guild", faction);
    }

    [Fact]
    public void RestoreState_IsNullTolerant()
    {
        var flags = new WorldFlagsService();
        flags.SetFlag("stale", true);

        // Wrong type, null payload, and null map all restore to an empty store without throwing (§16.3).
        flags.RestoreState("not a dto");
        Assert.False(flags.GetFlag("stale"));

        flags.SetFlag("stale", true);
        flags.RestoreState(null!);
        Assert.False(flags.GetFlag("stale"));

        flags.SetFlag("stale", true);
        flags.RestoreState(new WorldFlagsDto(null!));
        Assert.False(flags.GetFlag("stale"));
    }
}
