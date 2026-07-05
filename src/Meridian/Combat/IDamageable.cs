namespace Meridian.Combat;

/// <summary>
/// Interface implemented by any hittable component (character hulls, vehicle bodies, target dummies).
/// Enforces Section 6.1 requirements.
/// </summary>
public interface IDamageable
{
    void ApplyDamage(DamageInfo info);
}
