using Xunit;
using Meridian.Combat;
using Meridian.Items;

namespace Meridian.Tests.Combat;

/// <summary>
/// Exercises the real <see cref="WeaponRuntime"/> fire/reload cycle rather than re-implementing it
/// inline (T1).
/// </summary>
public class WeaponCombatTests
{
    private static (WeaponRuntime runtime, WeaponInstance instance, InventoryModel inv) MakeWeapon(
        int startAmmo, int reserve, int magazine = 10, float fireRate = 5f, float reloadTime = 1.0f)
    {
        var instance = new WeaponInstance("pistol_item", "pistol_definition") { CurrentAmmo = startAmmo };
        var def = new BasicWeaponDefinition
        {
            Id = "pistol_definition",
            MagazineSize = magazine,
            AmmoTypeId = "ammo_9mm",
            FireRate = fireRate,
            ReloadTime = reloadTime,
        };
        var inv = new InventoryModel();
        inv.RegisterDefinition(new BasicItemDefinition("ammo_9mm"));
        if (reserve > 0)
        {
            inv.AddItem(new ItemInstance("ammo_9mm", reserve));
        }
        return (new WeaponRuntime(instance, def, inv), instance, inv);
    }

    [Fact]
    public void TryFire_ShouldConsumeAmmoAndStartCooldown()
    {
        var (runtime, instance, _) = MakeWeapon(startAmmo: 10, reserve: 0);

        Assert.True(runtime.TryFire());
        Assert.Equal(9, instance.CurrentAmmo);

        // Cooldown blocks an immediate second shot.
        Assert.False(runtime.TryFire());
        Assert.Equal(9, instance.CurrentAmmo);
    }

    [Fact]
    public void TryFire_ShouldFail_WhenMagazineEmpty()
    {
        var (runtime, instance, _) = MakeWeapon(startAmmo: 0, reserve: 20);
        Assert.False(runtime.TryFire());
        Assert.Equal(0, instance.CurrentAmmo);
    }

    [Fact]
    public void Reload_ShouldDeductReservesAndRefillMagazine()
    {
        var (runtime, instance, inv) = MakeWeapon(startAmmo: 2, reserve: 20, magazine: 10, reloadTime: 1.0f);

        Assert.True(runtime.StartReload());
        Assert.True(runtime.IsReloading);

        runtime.Tick(1.0); // completes the reload

        Assert.False(runtime.IsReloading);
        Assert.Equal(10, instance.CurrentAmmo);        // magazine refilled 2 -> 10
        Assert.Equal(12, inv.GetItemCount("ammo_9mm")); // 20 - 8 = 12
    }

    [Fact]
    public void Reload_ShouldNotStart_WhenNoReserves()
    {
        var (runtime, _, _) = MakeWeapon(startAmmo: 2, reserve: 0);
        Assert.False(runtime.StartReload());
        Assert.False(runtime.IsReloading);
    }

    [Fact]
    public void Reload_ShouldNotStart_WhenMagazineFull()
    {
        var (runtime, _, _) = MakeWeapon(startAmmo: 10, reserve: 20, magazine: 10);
        Assert.False(runtime.StartReload());
    }
}
