using System;

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
}
