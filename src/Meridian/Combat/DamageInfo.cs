using Godot;

namespace Meridian.Combat;

/// <summary>
/// Hit zone identifiers for spatial target components.
/// </summary>
public enum HitZone
{
    Body,
    Head,
    Limbs,
    Weakpoint
}

/// <summary>
/// Immutable DTO conveying combat damage event metrics.
/// Enforces Section 6.1 requirements.
/// </summary>
public readonly struct DamageInfo
{
    public float Amount { get; }
    public string DamageTypeId { get; }
    public Node3D? SourceEntity { get; }
    public HitZone Zone { get; }
    public Vector3 HitPosition { get; }
    public Vector3 HitNormal { get; }
    public bool IsCritical { get; }

    public DamageInfo(float amount, string damageTypeId, Node3D? sourceEntity, HitZone zone, Vector3 hitPosition, Vector3 hitNormal, bool isCritical = false)
    {
        Amount = amount;
        DamageTypeId = string.IsNullOrEmpty(damageTypeId) ? "physical" : damageTypeId;
        SourceEntity = sourceEntity;
        Zone = zone;
        HitPosition = hitPosition;
        HitNormal = hitNormal;
        IsCritical = isCritical;
    }
}
