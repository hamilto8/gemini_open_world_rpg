using System;
using Meridian.Items;

namespace Meridian.Combat;

/// <summary>
/// Engine-free fire + reload state machine for a single equipped weapon. The <see cref="WeaponController"/>
/// Node owns raycasting and VFX and delegates ammo/cooldown/reload bookkeeping here, so the cycle is
/// unit-testable without instantiating a Godot Node (T1, Section 6.3 AmmoModule).
/// </summary>
public sealed class WeaponRuntime
{
    private readonly IWeaponDefinition _definition;
    private readonly WeaponInstance _instance;
    private readonly InventoryModel _inventory;

    private double _fireCooldown;
    private double _reloadTimer;

    public WeaponRuntime(WeaponInstance instance, IWeaponDefinition definition, InventoryModel inventory)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
    }

    public bool IsReloading { get; private set; }
    public int CurrentAmmo => _instance.CurrentAmmo;

    public double ReloadProgress =>
        IsReloading && _definition.ReloadTime > 0.0 ? 1.0 - (_reloadTimer / _definition.ReloadTime) : 0.0;

    public bool CanFire() => !IsReloading && _fireCooldown <= 0.0 && _instance.CurrentAmmo > 0;

    /// <summary>
    /// Consumes one round and starts the fire cooldown if able. Returns true when a shot was fired
    /// (the caller then performs the raycast/projectile spawn).
    /// </summary>
    public bool TryFire()
    {
        if (!CanFire()) return false;

        _instance.CurrentAmmo--;
        _fireCooldown = _definition.FireRate > 0.0f ? 1.0 / _definition.FireRate : 0.0;
        return true;
    }

    /// <summary>Begins a timed reload if reserves exist and the magazine isn't full. Returns true if started.</summary>
    public bool StartReload()
    {
        if (IsReloading) return false;

        int reserve = _inventory.GetItemCount(_definition.AmmoTypeId);
        if (reserve <= 0 || _instance.CurrentAmmo >= _definition.MagazineSize)
        {
            return false;
        }

        IsReloading = true;
        _reloadTimer = _definition.ReloadTime;
        return true;
    }

    /// <summary>Advances fire cooldown and reload timers; completes the reload when the timer elapses.</summary>
    public void Tick(double delta)
    {
        if (_fireCooldown > 0.0)
        {
            _fireCooldown = Math.Max(0.0, _fireCooldown - delta);
        }

        if (IsReloading)
        {
            _reloadTimer = Math.Max(0.0, _reloadTimer - delta);
            if (_reloadTimer <= 0.0)
            {
                CompleteReload();
            }
        }
    }

    private void CompleteReload()
    {
        IsReloading = false;

        int needed = _definition.MagazineSize - _instance.CurrentAmmo;
        int reserve = _inventory.GetItemCount(_definition.AmmoTypeId);
        int loaded = Math.Min(needed, reserve);

        if (loaded > 0)
        {
            _inventory.RemoveItem(_definition.AmmoTypeId, loaded);
            _instance.CurrentAmmo += loaded;
        }
    }
}
