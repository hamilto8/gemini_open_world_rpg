using Godot;

namespace Meridian.Combat;

/// <summary>
/// Implemented by damageable targets that classify a world-space hit into a <see cref="HitZone"/>
/// from their own anatomy/colliders. Per doc §6.1 hit-zone tags live on the target — not derived
/// from the shooter's camera height (which was the H1 bug).
/// </summary>
public interface IHitZoneResolver
{
    HitZone ResolveHitZone(Vector3 worldHitPosition);
}
