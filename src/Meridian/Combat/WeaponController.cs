using Godot;
using Meridian.Data;
using Meridian.Items;

namespace Meridian.Combat;

/// <summary>
/// Firing and reloading logic component attached to weapon visual nodes or characters.
/// Resolves shooting via 3D physics raycasting and implements magazine reload cycles.
/// Enforces Section 6.3 and 6.5 requirements.
/// </summary>
public partial class WeaponController : Node
{
    [Export] public WeaponResource? DefinitionResource { get; set; }

    private IWeaponDefinition? _definition;
    private WeaponInstance? _instance;
    private WeaponRuntime? _runtime;

    public IWeaponDefinition? Definition => _definition ?? DefinitionResource;
    public WeaponInstance? Instance => _instance;
    public bool IsReloading => _runtime?.IsReloading ?? false;
    public double ReloadProgress => _runtime?.ReloadProgress ?? 0.0;

    public override void _PhysicsProcess(double delta)
    {
        _runtime?.Tick(delta);
    }

    public void Initialize(WeaponInstance instance, IWeaponDefinition definition, InventoryModel inventory)
    {
        _instance = instance;
        _definition = definition;
        _runtime = new WeaponRuntime(instance, definition, inventory);
    }

    public bool CanFire() => _runtime?.CanFire() ?? false;

    public void Fire(Vector3 globalPosition, Vector3 targetDirection)
    {
        if (_runtime == null || Definition == null) return;

        // Ammo/cooldown bookkeeping lives in the runtime; only raycast when a round was actually fired.
        if (!_runtime.TryFire()) return;

        // Perform hitscan raycast (Section 6.3 DeliveryStrategy hitscan)
        PhysicsDirectSpaceState3D spaceState = GetViewport().FindWorld3D().DirectSpaceState;
        Vector3 endPosition = globalPosition + (targetDirection * Definition.MaxRange);

        var query = PhysicsRayQueryParameters3D.Create(globalPosition, endPosition);

        // Exclude the shooter's own body so a camera-anchored muzzle can't hit the firer (H2).
        var shooter = GetParentOrNull<Node3D>();
        if (shooter is CollisionObject3D shooterBody)
        {
            query.Exclude = new Godot.Collections.Array<Rid> { shooterBody.GetRid() };
        }

        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            var collider = (Node)result["collider"];
            var hitPosition = (Vector3)result["position"];
            var hitNormal = (Vector3)result["normal"];

            if (collider is IDamageable damageable)
            {
                // Derive the hit zone from the target's own anatomy, not the shooter's camera (H1).
                HitZone zone = collider is IHitZoneResolver resolver
                    ? resolver.ResolveHitZone(hitPosition)
                    : HitZone.Body;

                var damageInfo = new DamageInfo(
                    amount: Definition.BaseDamage,
                    damageTypeId: Definition.DamageTypeId,
                    sourceEntity: shooter,
                    zone: zone,
                    hitPosition: hitPosition,
                    hitNormal: hitNormal
                );

                damageable.ApplyDamage(damageInfo);
            }
        }
    }

    public void StartReload()
    {
        _runtime?.StartReload();
    }
}
