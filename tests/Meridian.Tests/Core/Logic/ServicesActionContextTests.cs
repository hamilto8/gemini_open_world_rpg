using Meridian.Core;
using Meridian.Core.Logic;
using Meridian.Core.Registry;
using Meridian.Items;
using Xunit;

namespace Meridian.Tests.Core.Logic;

public class ServicesActionContextTests
{
    private sealed class InventoryProvider : IInventoryProvider
    {
        public InventoryModel Inventory { get; } = new();
        public WeaponInstance? EquippedWeapon { get; set; }
    }

    [Fact]
    public void GiveItem_ResolvesAndRegistersAuthoredDefinition()
    {
        Services.Reset();
        try
        {
            var database = new ContentDatabase();
            database.LoadItems(new[] { new BasicItemDefinition("metal_scrap", 99, 0.1f) });
            var provider = new InventoryProvider();
            Services.Register<IContentDatabase>(database);
            Services.Register<IInventoryProvider>(provider);

            var context = new ServicesActionContext();

            Assert.True(context.GiveItem("metal_scrap", 2));
            Assert.Equal(2, provider.Inventory.GetItemCount("metal_scrap"));
        }
        finally
        {
            Services.Reset();
        }
    }

    [Fact]
    public void GiveItem_UnknownContentFailsAndWarns()
    {
        Services.Reset();
        try
        {
            var database = new ContentDatabase();
            var provider = new InventoryProvider();
            var warnings = new List<string>();
            Services.Register<IContentDatabase>(database);
            Services.Register<IInventoryProvider>(provider);

            var context = new ServicesActionContext(warnings.Add);

            Assert.False(context.GiveItem("typo_item", 1));
            Assert.Empty(provider.Inventory.Items);
            Assert.Contains(warnings, warning => warning.Contains("not registered"));
        }
        finally
        {
            Services.Reset();
        }
    }
}
