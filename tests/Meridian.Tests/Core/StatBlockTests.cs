using Xunit;
using Meridian.Core;

namespace Meridian.Tests.Core;

public class StatBlockTests
{
    [Fact]
    public void Modifier_OnUnregisteredStat_StillApplies()
    {
        // H3: previously GetStat short-circuited to 0 for stats without a base value, silently
        // dropping the modifier. A flat additive modifier on an unregistered stat should now apply.
        var stats = new StatBlock();
        Assert.False(stats.HasBaseStat("recoil_reduction"));

        stats.AddModifier(new Modifier("recoil_reduction", ModifierOp.Add, 3f, "gear_grip"));

        Assert.Equal(3f, stats.GetStat("recoil_reduction"), 3);
    }

    [Fact]
    public void ReloadSpeed_IsRegisteredByDefault_ForPerkScaling()
    {
        var stats = new StatBlock();
        Assert.True(stats.HasBaseStat("reload_speed"));
        Assert.Equal(1.0f, stats.GetStat("reload_speed"), 3);
    }

    [Fact]
    public void DirtyCache_ShouldRecompute_AfterBaseChange()
    {
        var stats = new StatBlock();
        stats.SetBaseStat("armor", 5f);
        Assert.Equal(5f, stats.GetStat("armor"), 3);

        stats.SetBaseStat("armor", 12f);
        Assert.Equal(12f, stats.GetStat("armor"), 3);
    }

    [Fact]
    public void RemoveModifierBySource_ShouldRemoveAllFromThatSource()
    {
        var stats = new StatBlock();
        stats.SetBaseStat("armor", 0f);
        stats.AddModifier(new Modifier("armor", ModifierOp.Add, 10f, "perk_thick_skin"));
        stats.AddModifier(new Modifier("armor", ModifierOp.Add, 5f, "perk_thick_skin"));
        Assert.Equal(15f, stats.GetStat("armor"), 3);

        stats.RemoveModifierBySource("perk_thick_skin");

        Assert.Equal(0f, stats.GetStat("armor"), 3);
    }

    [Fact]
    public void TickModifiers_ShouldRemoveExpiredModifiers()
    {
        var stats = new StatBlock();
        stats.SetBaseStat("move_speed", 5f);
        stats.AddModifier(new Modifier("move_speed", ModifierOp.Add, 2f, "sprint_buff", expiryTime: 100.0));
        Assert.Equal(7f, stats.GetStat("move_speed"), 3);

        stats.TickModifiers(50.0); // not yet expired
        Assert.Equal(7f, stats.GetStat("move_speed"), 3);

        stats.TickModifiers(100.0); // expires at >= 100
        Assert.Equal(5f, stats.GetStat("move_speed"), 3);
    }
}
