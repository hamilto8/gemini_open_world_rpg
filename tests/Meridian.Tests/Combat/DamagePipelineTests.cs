using Xunit;
using Godot;
using Meridian.Combat;
using Meridian.Core;

namespace Meridian.Tests.Combat;

/// <summary>
/// Exercises the real <see cref="DamagePipeline"/> (previously this asserted inline arithmetic that
/// never touched production code — T1).
/// </summary>
public class DamagePipelineTests
{
    [Fact]
    public void Mitigate_ShouldApplyHeadMultiplierThenArmor()
    {
        // 50 * 2.0 (head) - 10 armor = 90
        Assert.Equal(90f, DamagePipeline.Mitigate(50f, HitZone.Head, 10f), 3);
    }

    [Fact]
    public void Mitigate_ShouldApplyLimbReduction()
    {
        // 50 * 0.5 (limb) - 5 armor = 20
        Assert.Equal(20f, DamagePipeline.Mitigate(50f, HitZone.Limbs, 5f), 3);
    }

    [Fact]
    public void Mitigate_ShouldClampToMinimumOfOne_WhenArmorExceedsRaw()
    {
        Assert.Equal(1f, DamagePipeline.Mitigate(5f, HitZone.Body, 999f), 3);
    }

    [Theory]
    [InlineData(HitZone.Body, 1.0f)]
    [InlineData(HitZone.Head, 2.0f)]
    [InlineData(HitZone.Limbs, 0.5f)]
    [InlineData(HitZone.Weakpoint, 3.0f)]
    public void ZoneMultiplier_ShouldMatchDesign(HitZone zone, float expected)
    {
        Assert.Equal(expected, DamagePipeline.ZoneMultiplier(zone), 3);
    }

    [Fact]
    public void Apply_ShouldUseSharedStatsAndReportLifecycleTransition()
    {
        var stats = new StatBlock();
        stats.SetBaseStat("health", 25f);
        stats.SetBaseStat("armor", 5f);
        var hit = new DamageInfo(20f, "physical", null, HitZone.Body, Vector3.Zero, Vector3.Up);

        DamageApplicationResult first = DamagePipeline.Apply(stats, hit);
        DamageApplicationResult second = DamagePipeline.Apply(stats, hit);

        Assert.True(first.WasApplied);
        Assert.Equal(10f, first.NewHealth, 3);
        Assert.True(second.IsDead);
        Assert.Equal(0f, second.NewHealth, 3);
        Assert.Equal(0f, stats.GetStat("health"), 3);
    }
}
