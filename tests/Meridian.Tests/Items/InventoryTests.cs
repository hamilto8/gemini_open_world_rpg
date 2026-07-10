using System;
using System.Collections.Generic;
using Xunit;
using Meridian.Core;
using Meridian.Items;

namespace Meridian.Tests.Items;

public class InventoryTests : IDisposable
{
    public InventoryTests()
    {
        Services.Reset();
    }

    public void Dispose()
    {
        Services.Reset();
    }

    private class MockEquippableBehavior : IEquippableBehavior
    {
        public string SlotId { get; set; } = "chest";
        public string TargetStatId { get; set; } = "armor";
        public float ModifierValue { get; set; } = 15.0f;
    }

    [Fact]
    public void InventoryModel_ShouldRespectWeightLimits()
    {
        var inventory = new InventoryModel { MaxWeight = 10.0f };
        var heavyDefinition = new BasicItemDefinition("iron_ore", 99, 4.0f);
        inventory.RegisterDefinition(heavyDefinition);

        // Add 2 iron ore = 8.0 lbs (valid)
        Assert.True(inventory.AddItem(new ItemInstance("iron_ore", 2)));
        Assert.Equal(8.0f, inventory.CalculateTotalWeight());

        // Add 1 more iron ore = 12.0 lbs (fails limit)
        Assert.False(inventory.AddItem(new ItemInstance("iron_ore", 1)));
        Assert.Equal(2, inventory.GetItemCount("iron_ore"));
    }

    [Fact]
    public void EquipmentModel_ShouldPushModifiersToStatBlock()
    {
        var stats = new StatBlock();
        var equip = new EquipmentModel();
        
        var armorBehavior = new MockEquippableBehavior();
        var chestDefinition = new BasicItemDefinition("leather_armor")
        {
            Behaviors = new List<object> { armorBehavior }
        };
        equip.RegisterDefinition(chestDefinition);

        Assert.Equal(0f, stats.GetStat("armor"));

        var armorInstance = new ItemInstance("leather_armor");
        Assert.True(equip.EquipItem("chest", armorInstance, stats));

        // Verify armor stat increased by modifier value
        Assert.Equal(15.0f, stats.GetStat("armor"));

        // Unequip
        Assert.True(equip.UnequipItem("chest", stats));
        Assert.Equal(0f, stats.GetStat("armor"));
    }

    [Fact]
    public void InventoryTransaction_ShouldRollbackOnFailure()
    {
        var inventory = new InventoryModel { MaxWeight = 10.0f, AllowUnregisteredItems = true };

        // Setup initial inventory count
        inventory.AddItem(new ItemInstance("metal_scrap", 5));
        Assert.Equal(5, inventory.GetItemCount("metal_scrap"));

        var transaction = new InventoryTransaction();
        transaction.DeductItem(inventory, "metal_scrap", 3);
        
        // This add will fail because item_heavy exceeds max weight (15 lbs)
        var heavyItem = new ItemInstance("heavy_item", 1);
        inventory.RegisterDefinition(new BasicItemDefinition("heavy_item", 99, 15.0f));
        transaction.AddItem(inventory, heavyItem);

        bool success = transaction.Execute();
        Assert.False(success);

        // Inventory should remain completely unchanged (rollback of deduct)
        Assert.Equal(5, inventory.GetItemCount("metal_scrap"));
        Assert.Equal(0, inventory.GetItemCount("heavy_item"));
    }

    [Fact]
    public void Transaction_Rollback_ShouldPreserveUniqueItemPayload()
    {
        // M17: a deducted unique item must be restored as its real instance (payload intact),
        // not recreated as a plain ItemInstance.
        var inventory = new InventoryModel { MaxWeight = 100.0f };
        inventory.RegisterDefinition(new BasicItemDefinition("rifle", maxStack: 1, weight: 3.0f));
        inventory.RegisterDefinition(new BasicItemDefinition("heavy_item", 99, 200.0f));

        var rifle = new WeaponInstance("rifle", "rifle_def") { CurrentAmmo = 30, UpgradeLevel = 4 };
        rifle.InstalledModIds.Add("suppressor");
        Assert.True(inventory.AddItem(rifle));

        var transaction = new InventoryTransaction();
        transaction.DeductItem(inventory, "rifle", 1);
        transaction.AddItem(inventory, new ItemInstance("heavy_item", 1)); // forces failure -> rollback
        Assert.False(transaction.Execute());

        // The exact rifle instance (with ammo, upgrade level, mods) is back.
        var restored = Assert.IsType<WeaponInstance>(Assert.Single(inventory.Items));
        Assert.Same(rifle, restored);
        Assert.Equal(30, restored.CurrentAmmo);
        Assert.Equal(4, restored.UpgradeLevel);
        Assert.Contains("suppressor", restored.InstalledModIds);
    }

    [Fact]
    public void AddItem_ShouldSplitAcrossStacksAtMaxStackBoundary()
    {
        var inventory = new InventoryModel { MaxWeight = 1000f };
        inventory.RegisterDefinition(new BasicItemDefinition("bullet", maxStack: 30, weight: 0.0f));

        Assert.True(inventory.AddItem(new ItemInstance("bullet", 25)));
        Assert.True(inventory.AddItem(new ItemInstance("bullet", 20))); // 25 + 20 = 45 => 30 + 15

        Assert.Equal(45, inventory.GetItemCount("bullet"));
        Assert.Equal(2, inventory.Items.Count); // split into two stacks at the 30 boundary
    }

    [Fact]
    public void RemoveItem_ShouldSpanMultipleStacks()
    {
        var inventory = new InventoryModel { MaxWeight = 1000f };
        inventory.RegisterDefinition(new BasicItemDefinition("bullet", maxStack: 30, weight: 0.0f));
        inventory.AddItem(new ItemInstance("bullet", 30));
        inventory.AddItem(new ItemInstance("bullet", 30));

        Assert.True(inventory.RemoveItem("bullet", 45)); // drains one full stack + part of another
        Assert.Equal(15, inventory.GetItemCount("bullet"));
    }

    [Fact]
    public void AddItem_UnregisteredDefinition_FailsUnlessAllowed()
    {
        // L7: production must not silently stub unknown item definitions.
        var strict = new InventoryModel();
        Assert.False(strict.AddItem(new ItemInstance("mystery", 1)));
        Assert.Equal(0, strict.GetItemCount("mystery"));

        var lenient = new InventoryModel { AllowUnregisteredItems = true };
        Assert.True(lenient.AddItem(new ItemInstance("mystery", 1)));
        Assert.Equal(1, lenient.GetItemCount("mystery"));
    }

    [Fact]
    public void AddItem_WhenOverWeight_FailsWithoutMutatingInventory()
    {
        // Pickups rely on this contract: a false return must mean "nothing happened", so the pickup
        // node can safely stay in the world instead of destroying the item (pickup item-loss fix).
        var inventory = new InventoryModel { MaxWeight = 10.0f };
        inventory.RegisterDefinition(new BasicItemDefinition("iron_ore", 99, 4.0f));
        Assert.True(inventory.AddItem(new ItemInstance("iron_ore", 2))); // 8.0 of 10.0

        bool changed = false;
        inventory.InventoryChanged += () => changed = true;

        // Would land partly in the existing stack — must be rejected atomically, not partially added.
        Assert.False(inventory.AddItem(new ItemInstance("iron_ore", 2)));

        Assert.False(changed);
        Assert.Equal(2, inventory.GetItemCount("iron_ore"));
        Assert.Equal(8.0f, inventory.CalculateTotalWeight(), 3);
    }
}
