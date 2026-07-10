using System.Collections.Generic;
using System.Linq;
using Xunit;
using Meridian.Combat;
using Meridian.Core.Registry;
using Meridian.Data;
using Meridian.Items;

namespace Meridian.Tests.Core;

/// <summary>
/// Covers the engine-free registry core: id-keyed lookup, first-wins duplicate policy with diagnostics,
/// GetRequired throwing, deterministic insertion order, case-insensitive lookup (§19.9), and
/// ContentDatabase category loading via plain C# fakes (ADR-0003).
/// </summary>
public class RegistryTests
{
    [Fact]
    public void Register_ThenTryGet_ReturnsSameDefinition()
    {
        var registry = new Registry<IItemDefinition>();
        var medkit = new BasicItemDefinition("medkit");

        bool registered = registry.Register("medkit", medkit);

        Assert.True(registered);
        Assert.Equal(1, registry.Count);
        Assert.True(registry.TryGet("medkit", out var found));
        Assert.Same(medkit, found);
        Assert.True(registry.Contains("medkit"));
    }

    [Fact]
    public void Register_DuplicateId_FirstWins_AndRecordsDiagnostic()
    {
        var registry = new Registry<IItemDefinition>();
        var first = new BasicItemDefinition("medkit");
        var second = new BasicItemDefinition("medkit");

        Assert.True(registry.Register("medkit", first));
        bool secondResult = registry.Register("medkit", second);

        Assert.False(secondResult);
        Assert.Equal(1, registry.Count);
        Assert.Same(first, registry.GetRequired("medkit")); // first entry wins
        Assert.Contains(registry.Diagnostics, d => d.Contains("Duplicate id 'medkit'"));
    }

    [Fact]
    public void Register_EmptyId_Rejected_WithDiagnostic()
    {
        var registry = new Registry<IItemDefinition>();

        bool result = registry.Register("", new BasicItemDefinition(""));

        Assert.False(result);
        Assert.Equal(0, registry.Count);
        Assert.Contains(registry.Diagnostics, d => d.Contains("empty id"));
    }

    [Fact]
    public void GetRequired_UnknownId_ThrowsWithClearMessage()
    {
        var registry = new Registry<IItemDefinition>();

        var ex = Assert.Throws<KeyNotFoundException>(() => registry.GetRequired("ghost"));

        Assert.Contains("ghost", ex.Message);
        Assert.Contains("IItemDefinition", ex.Message);
    }

    [Fact]
    public void Entries_PreserveInsertionOrder()
    {
        var registry = new Registry<IItemDefinition>();
        registry.Register("charlie", new BasicItemDefinition("charlie"));
        registry.Register("alpha", new BasicItemDefinition("alpha"));
        registry.Register("bravo", new BasicItemDefinition("bravo"));

        var order = registry.Entries.Select(e => e.Key).ToList();

        Assert.Equal(new[] { "charlie", "alpha", "bravo" }, order);
    }

    [Fact]
    public void Lookup_IsCaseInsensitive()
    {
        var registry = new Registry<IItemDefinition>();
        registry.Register("medkit", new BasicItemDefinition("medkit"));

        Assert.True(registry.TryGet("MEDKIT", out _));
        Assert.True(registry.Contains("MedKit"));

        // Case-insensitive comparison also detects a duplicate that differs only in case.
        bool dup = registry.Register("MEDKIT", new BasicItemDefinition("MEDKIT"));
        Assert.False(dup);
    }

    [Fact]
    public void ContentDatabase_LoadCategories_PopulatesRegistries()
    {
        var db = new ContentDatabase();

        db.LoadItems(new IItemDefinition?[] { new BasicItemDefinition("medkit"), new BasicItemDefinition("ammo_9mm") });
        db.LoadWeapons(new IWeaponDefinition?[] { new BasicWeaponDefinition { Id = "pistol" } });
        db.LoadWeatherProfiles(new IWeatherProfile?[] { new BasicWeatherProfile("clear") });
        db.LoadHandlingProfiles(new IVehicleHandlingProfile?[] { new BasicVehicleHandlingProfile { Id = "sedan" } });

        Assert.Equal(2, db.Items.Count);
        Assert.True(db.Items.Contains("medkit"));
        Assert.True(db.Items.Contains("ammo_9mm"));
        Assert.Equal(1, db.Weapons.Count);
        Assert.Equal("pistol", db.Weapons.GetRequired("pistol").Id);
        Assert.Equal(1, db.WeatherProfiles.Count);
        Assert.Equal(1, db.HandlingProfiles.Count);
        Assert.Empty(db.Diagnostics);
    }

    [Fact]
    public void ContentDatabase_NullFeed_IsEmptyCategory_NoDiagnostics()
    {
        var db = new ContentDatabase();

        db.LoadItems(null); // a null index slot == empty category (§19.1)

        Assert.Equal(0, db.Items.Count);
        Assert.Empty(db.Diagnostics);
    }

    [Fact]
    public void ContentDatabase_NullEntryAndDuplicate_ReportedWithCategoryTag()
    {
        var db = new ContentDatabase();

        db.LoadItems(new IItemDefinition?[]
        {
            new BasicItemDefinition("medkit"),
            null,                                  // unassigned index slot (§19.10)
            new BasicItemDefinition("medkit"),     // duplicate within category (§19.9)
        });

        Assert.Equal(1, db.Items.Count);
        Assert.Contains(db.Diagnostics, d => d.Contains("[Items]") && d.Contains("null entry"));
        Assert.Contains(db.Diagnostics, d => d.Contains("[Items]") && d.Contains("Duplicate id 'medkit'"));
    }

    [Fact]
    public void ContentDatabase_SameIdAcrossCategories_IsLegal()
    {
        var db = new ContentDatabase();

        // The same snake_case id in two different categories must NOT collide (§19.9).
        db.LoadItems(new IItemDefinition?[] { new BasicItemDefinition("relay") });
        db.LoadLootTables(new ILootTableDefinition?[] { new BasicLootTableDefinition("relay") });

        Assert.True(db.Items.Contains("relay"));
        Assert.True(db.LootTables.Contains("relay"));
        Assert.Empty(db.Diagnostics);
    }
}
