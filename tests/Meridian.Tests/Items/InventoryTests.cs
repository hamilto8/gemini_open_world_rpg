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
        var inventory = new InventoryModel { MaxWeight = 10.0f };
        
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
}
