using System.Collections.Generic;
using Meridian.Core.Save;
using Meridian.Items;
using Xunit;

namespace Meridian.Tests.Core;

public class InventorySaveParticipantTests
{
    [Fact]
    public void CaptureRestore_RoundTripsInventoryAndEquippedWeapon()
    {
        var definitions = new Dictionary<string, IItemDefinition>
        {
            ["ammo_9mm"] = new BasicItemDefinition("ammo_9mm", maxStack: 30, weight: 0.01f),
            ["test_pistol_item"] = new BasicItemDefinition("test_pistol_item", maxStack: 1, weight: 2f),
        };

        var source = new InventoryModel { MaxWeight = 72f };
        source.RegisterDefinition(definitions["ammo_9mm"]);
        source.AddItem(new ItemInstance("ammo_9mm", 47));
        var equipped = new WeaponInstance("test_pistol_item", "test_pistol")
        {
            CurrentAmmo = 8,
            UpgradeLevel = 3,
        };
        equipped.InstalledModIds.Add("quiet_muzzle");

        var writer = new InventorySaveParticipant(
            source,
            id => definitions.GetValueOrDefault(id),
            () => equipped,
            _ => { });
        var dto = writer.CaptureState();

        var restoredInventory = new InventoryModel();
        WeaponInstance? restoredWeapon = null;
        var reader = new InventorySaveParticipant(
            restoredInventory,
            id => definitions.GetValueOrDefault(id),
            () => restoredWeapon,
            value => restoredWeapon = value);
        reader.RestoreState(dto);

        Assert.Equal(72f, restoredInventory.MaxWeight);
        Assert.Equal(47, restoredInventory.GetItemCount("ammo_9mm"));
        Assert.NotNull(restoredWeapon);
        Assert.Equal("test_pistol", restoredWeapon.WeaponDefinitionId);
        Assert.Equal(8, restoredWeapon.CurrentAmmo);
        Assert.Equal(3, restoredWeapon.UpgradeLevel);
        Assert.Contains("quiet_muzzle", restoredWeapon.InstalledModIds);
    }

    [Fact]
    public void Restore_UnknownDefinitionPreservesItemAsPlaceholder()
    {
        var inventory = new InventoryModel();
        var warnings = new List<string>();
        var participant = new InventorySaveParticipant(
            inventory,
            _ => null,
            () => null,
            _ => { },
            warnings.Add);
        var dto = new InventoryStateDto(
            50f,
            new List<ItemInstanceDto>
            {
                new("removed_dlc_item", 2, new Dictionary<string, string>(), null, 0, 0, new List<string>()),
            },
            null);

        participant.RestoreState(dto);

        Assert.Equal(2, inventory.GetItemCount("removed_dlc_item"));
        Assert.Contains(warnings, message => message.Contains("placeholder"));
    }
}
