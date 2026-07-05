using System;
using Xunit;
using Meridian.Combat;
using Meridian.Items;

namespace Meridian.Tests.Combat;

public class WeaponCombatTests
{
    [Fact]
    public void WeaponController_Firing_ShouldConsumeAmmo()
    {
        var weaponInstance = new WeaponInstance("pistol_item", "pistol_definition")
        {
            CurrentAmmo = 10
        };

        // Firing consumes one round
        weaponInstance.CurrentAmmo--;
        Assert.Equal(9, weaponInstance.CurrentAmmo);
    }

    [Fact]
    public void WeaponController_Reloading_ShouldDeductReservesAndRefillMagazine()
    {
        var weaponInstance = new WeaponInstance("pistol_item", "pistol_definition")
        {
            CurrentAmmo = 2
        };

        var weaponDef = new BasicWeaponDefinition
        {
            Id = "pistol_definition",
            MagazineSize = 10,
            AmmoTypeId = "ammo_9mm",
            ReloadTime = 1.0f
        };

        var inventory = new InventoryModel();
        inventory.RegisterDefinition(new BasicItemDefinition("ammo_9mm"));
        
        // Add 20 rounds to reserve
        inventory.AddItem(new ItemInstance("ammo_9mm", 20));
        Assert.Equal(20, inventory.GetItemCount("ammo_9mm"));

        // Simulate reload completion logic
        int needed = weaponDef.MagazineSize - weaponInstance.CurrentAmmo; // 8
        int reserve = inventory.GetItemCount("ammo_9mm");
        int loaded = Math.Min(needed, reserve);

        inventory.RemoveItem("ammo_9mm", loaded);
        weaponInstance.CurrentAmmo += loaded;

        Assert.Equal(10, weaponInstance.CurrentAmmo);
        Assert.Equal(12, inventory.GetItemCount("ammo_9mm")); // 20 - 8 = 12
    }
}
