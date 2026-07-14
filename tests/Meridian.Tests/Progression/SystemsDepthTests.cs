using System;
using Xunit;
using Meridian.Audio;
using Meridian.Core;
using Meridian.Data;
using Meridian.World;

namespace Meridian.Tests.Progression;

public class SystemsDepthTests
{
    [Fact]
    public void ProgressionManager_ShouldHandleLevelUpsAndGrantsPerkStatModifiers()
    {
        var profile = new BasicProgressionProfile
        {
            BaseXpRequired = 100,
            XpExponent = 1.0f, // Linear progression: 100 * level
            MaxLevel = 10
        };

        var manager = new ProgressionManager(profile);
        var stats = new StatBlock();
        // No reload_speed setup here: production StatBlock registers it (base 1.0), so the
        // fast_reload perk works against real defaults rather than test-only scaffolding (H3).

        Assert.Equal(1, manager.Level);
        Assert.Equal(0, manager.SkillPoints);

        // Add 100 XP -> level up to 2 (needs 100 XP)
        manager.AddXp(100);
        Assert.Equal(2, manager.Level);
        Assert.Equal(2, manager.SkillPoints);

        // Unlock fast_reload perk
        Assert.True(manager.UnlockPerk("fast_reload", stats));
        Assert.Equal(1, manager.SkillPoints);
        Assert.Contains("fast_reload", manager.UnlockedPerks);

        // Verify +15% reload_speed modifier is active
        Assert.Equal(1.15f, stats.GetStat("reload_speed"));
    }

    [Fact]
    public void FastTravelNetwork_ShouldValidateDiscoveryAndDiscoveryEvents()
    {
        var network = new FastTravelNetwork();
        string nodeId = "terminal_harbor";

        network.RegisterNode(nodeId, "Harbor Town Gate", new Godot.Vector3(100, 0, 100));

        Assert.False(network.CanTravelTo(nodeId));

        // Discover
        Assert.True(network.DiscoverNode(nodeId));
        Assert.True(network.CanTravelTo(nodeId));
    }

    [Fact]
    public void MusicManager_ShouldComputeCrossfadeVolumesDependingOnTension()
    {
        var manager = new MusicManager();

        // 0 tension (Exploration only)
        manager.SetTension(0f);
        Assert.Equal(0f, manager.ExplorationVolumeDb);
        Assert.Equal(-80f, manager.CombatVolumeDb);

        // 0.5 tension (Mixed stems)
        manager.SetTension(0.5f);
        Assert.Equal(-20f, manager.ExplorationVolumeDb);
        Assert.Equal(-40f, manager.CombatVolumeDb);

        // 1.0 tension (Combat only)
        manager.SetTension(1.0f);
        Assert.Equal(-40f, manager.ExplorationVolumeDb);
        Assert.Equal(0f, manager.CombatVolumeDb);
    }
}
