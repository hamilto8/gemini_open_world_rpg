using System;
using Meridian.Core;

namespace Meridian.Combat;

/// <summary>
/// Engine-free damage mitigation math (Section 6.1). Kept out of the Node layer so the ordered
/// pipeline is directly unit-testable rather than reachable only through a live scene (T1).
/// </summary>
public static class DamagePipeline
{
    /// <summary>Hit-zone damage multiplier (head/weakpoint amplify, limbs reduce).</summary>
    public static float ZoneMultiplier(HitZone zone) => zone switch
    {
        HitZone.Head => 2.0f,
        HitZone.Limbs => 0.5f,
        HitZone.Weakpoint => 3.0f,
        _ => 1.0f,
    };

    /// <summary>
    /// Ordered mitigation: hit-zone multiplier, then flat armor, clamped to a minimum of 1.0
    /// so any clean hit always registers some damage.
    /// </summary>
    public static float Mitigate(float amount, HitZone zone, float armor)
    {
        float raw = amount * ZoneMultiplier(zone);
        return Math.Max(1.0f, raw - armor);
    }

    /// <summary>
    /// Applies the canonical mitigation/lifecycle calculation to any entity StatBlock. Players,
    /// NPCs, dummies, and stat-backed vehicles use this one path rather than reimplementing combat.
    /// </summary>
    public static DamageApplicationResult Apply(StatBlock stats, DamageInfo info)
    {
        ArgumentNullException.ThrowIfNull(stats);

        float previousHealth = Math.Max(0f, stats.GetStat("health"));
        if (previousHealth <= 0f)
        {
            return new DamageApplicationResult(0f, 0f, true, false);
        }

        float applied = Mitigate(Math.Max(0f, info.Amount), info.Zone, Math.Max(0f, stats.GetStat("armor")));
        float newHealth = Math.Max(0f, previousHealth - applied);
        stats.SetBaseStat("health", newHealth);
        return new DamageApplicationResult(applied, newHealth, newHealth <= 0f, true);
    }
}

public readonly record struct DamageApplicationResult(
    float AppliedDamage,
    float NewHealth,
    bool IsDead,
    bool WasApplied);
