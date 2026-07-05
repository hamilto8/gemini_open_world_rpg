using Godot;
using System;
using Meridian.Core;
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
    private InventoryModel? _inventory;
    private double _fireCooldown = 0.0;
    private double _reloadTimer = 0.0;
    private bool _isReloading = false;

    public IWeaponDefinition? Definition => _definition ?? DefinitionResource;
    public WeaponInstance? Instance => _instance;
    public bool IsReloading => _isReloading;
    public double ReloadProgress => _isReloading && Definition != null ? 1.0 - (_reloadTimer / Definition.ReloadTime) : 0.0;

    public override void _PhysicsProcess(double delta)
    {
        if (_fireCooldown > 0.0)
        {
            _fireCooldown = Math.Max(0.0, _fireCooldown - delta);
        }

        if (_isReloading && Definition != null)
        {
            _reloadTimer = Math.Max(0.0, _reloadTimer - delta);
            if (_reloadTimer <= 0.0)
            {
                CompleteReload();
            }
        }
    }

    public void Initialize(WeaponInstance instance, IWeaponDefinition definition, InventoryModel inventory)
    {
        _instance = instance;
        _definition = definition;
        _inventory = inventory;
        _isReloading = false;
        _reloadTimer = 0.0;
    }

    public bool CanFire()
    {
        if (Definition == null || _instance == null) return false;
        if (_isReloading) return false;
        if (_fireCooldown > 0.0) return false;
        
        return _instance.CurrentAmmo > 0;
    }

    public void Fire(Vector3 globalPosition, Vector3 targetDirection)
    {
        if (!CanFire() || Definition == null || _instance == null) return;

        // Consume ammo
        _instance.CurrentAmmo--;

        // Trigger fire cooldown
        _fireCooldown = 1.0 / Definition.FireRate;

        // Perform hitscan raycast (Section 6.3 DeliveryStrategy hitscan)
        PhysicsDirectSpaceState3D spaceState = GetViewport().FindWorld3D().DirectSpaceState;
        Vector3 endPosition = globalPosition + (targetDirection * Definition.MaxRange);
        
        var query = PhysicsRayQueryParameters3D.Create(globalPosition, endPosition);
        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            var collider = (Node)result["collider"];
            var hitPosition = (Vector3)result["position"];
            var hitNormal = (Vector3)result["normal"];

            GD.Print($"[WeaponController] Shot hit target '{collider.Name}' at {hitPosition}");

            if (collider is IDamageable damageable)
            {
                // Simple Headshot helper check based on name/height (Section 6.1 Weakpoint)
                HitZone zone = HitZone.Body;
                if (hitPosition.Y - collider.GetViewport().GetCamera3D().GlobalPosition.Y > 0.5f)
                {
                    zone = HitZone.Head;
                }

                var damageInfo = new DamageInfo(
                    amount: Definition.BaseDamage,
                    damageTypeId: Definition.DamageTypeId,
                    sourceEntity: GetParentOrNull<Node3D>(),
                    zone: zone,
                    hitPosition: hitPosition,
                    hitNormal: hitNormal
                );
                
                damageable.ApplyDamage(damageInfo);
            }
        }
        else
        {
            GD.Print("[WeaponController] Shot missed (no collider hit).");
        }
    }

    public void StartReload()
    {
        if (Definition == null || _instance == null || _inventory == null) return;
        if (_isReloading) return;

        // Check if there is reserve ammo in inventory (Section 6.3 AmmoModule reserves)
        int reserve = _inventory.GetItemCount(Definition.AmmoTypeId);
        if (reserve <= 0 || _instance.CurrentAmmo >= Definition.MagazineSize)
        {
            return; // No reserve ammo, or magazine already full
        }

        _isReloading = true;
        _reloadTimer = Definition.ReloadTime;
        GD.Print($"[WeaponController] Reloading weapon '{Definition.DisplayName}'... (duration: {Definition.ReloadTime}s)");
    }

    private void CompleteReload()
    {
        if (Definition == null || _instance == null || _inventory == null) return;

        _isReloading = false;
        int needed = Definition.MagazineSize - _instance.CurrentAmmo;
        int reserve = _inventory.GetItemCount(Definition.AmmoTypeId);
        int loaded = Math.Min(needed, reserve);

        if (loaded > 0)
        {
            // Atomically deduct reserves and reload magazine
            _inventory.RemoveItem(Definition.AmmoTypeId, loaded);
            _instance.CurrentAmmo += loaded;
            GD.Print($"[WeaponController] Reload complete. Magazine: {_instance.CurrentAmmo}/{Definition.MagazineSize}, Reserves left: {reserve - loaded}");
        }
    }
}
