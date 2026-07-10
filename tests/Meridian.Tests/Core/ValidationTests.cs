using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Meridian.Combat;
using Meridian.Core;
using Meridian.Core.Registry;
using Meridian.Core.Validation;
using Meridian.Data;
using Meridian.Items;

namespace Meridian.Tests.Core;

/// <summary>
/// Covers the registry-based ContentValidator (§19.10): per-category duplicate ids (B2 fix — cross-category
/// reuse is legal, §19.9), snake_case policy, cross-reference resolution (weapon→ammo, loot→item, region
/// cell scenes), and the filesystem sweep (directory structure, dangling ext_resource paths, index orphan
/// detection). Database checks run on plain C# fakes; filesystem checks run against a temp directory —
/// Godot Resources cannot be instantiated headlessly (ADR-0003).
/// </summary>
public class ValidationTests : IDisposable
{
    private readonly string _tempProjectDir;

    public ValidationTests()
    {
        _tempProjectDir = Path.Combine(Path.GetTempPath(), "MeridianValidationTests_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempProjectDir))
        {
            Directory.Delete(_tempProjectDir, true);
        }
    }

    // ---- Filesystem sweep (ported from the pre-registry validator, same intent) --------------------------

    [Fact]
    public void ValidateContent_ShouldReturnErrorsForMissingDirectories()
    {
        // Empty folder without the standard structure. Legacy constructor: no IContentDatabase is
        // registered in Services, so only the filesystem sweep runs — and it must still fail.
        Directory.CreateDirectory(_tempProjectDir);

        var validator = new ContentValidator(_tempProjectDir);
        bool success = validator.ValidateContent(out var errors);

        Assert.False(success);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Missing 'assets/'"));
        Assert.Contains(errors, e => e.Contains("Missing 'src/'"));
    }

    [Fact]
    public void ValidateContent_ShouldPassWhenStructureIsCorrect()
    {
        CreateStandardStructure();

        var validator = new ContentValidator(_tempProjectDir);
        bool success = validator.ValidateContent(out var errors);

        Assert.True(success);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateContent_ShouldDetectDanglingExtResourceReference()
    {
        CreateStandardStructure();
        WriteDataResource("weapons/pistol.tres",
            "[gd_resource type=\"Resource\" load_steps=2 format=3]\n" +
            "[ext_resource type=\"Texture2D\" path=\"res://assets/missing_icon.png\" id=\"1\"]\n" +
            "[resource]\nId = \"pistol\"\n");
        WriteIndexReferencing("weapon_index.tres", "res://data/weapons/pistol.tres");

        var validator = new ContentValidator(_tempProjectDir);
        bool success = validator.ValidateContent(out var errors);

        Assert.False(success);
        Assert.Contains(errors, e => e.Contains("Dangling reference") && e.Contains("missing_icon.png"));
    }

    // Ported intent of the old duplicate-content-id test: duplicates are now detected per category in the
    // registry, not by text-scraping .tres files (which misparsed any '*id'-suffixed field, B1).
    [Fact]
    public void ValidateContent_ShouldDetectDuplicateIdsWithinCategory()
    {
        var db = new ContentDatabase();
        db.LoadItems(new IItemDefinition?[]
        {
            new BasicItemDefinition("pistol"),
            new BasicItemDefinition("pistol"),
        });

        var validator = new ContentValidator(db);
        bool success = validator.ValidateContent(out var errors);

        Assert.False(success);
        Assert.Contains(errors, e => e.Contains("Duplicate id 'pistol'"));
    }

    // ---- Registry checks (§19.9, §19.10) -----------------------------------------------------------------

    // Pins the B2 fix: ids are unique WITHIN a category; the same id in two categories is legal (§19.9).
    [Fact]
    public void ValidateContent_SameIdAcrossCategories_Passes()
    {
        var db = new ContentDatabase();
        db.LoadItems(new IItemDefinition?[] { new BasicItemDefinition("relay") });
        db.LoadLootTables(new ILootTableDefinition?[] { new BasicLootTableDefinition("relay", "relay") });

        var validator = new ContentValidator(db);
        bool success = validator.ValidateContent(out var errors);

        Assert.True(success);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateContent_EmptyId_Fails()
    {
        var db = new ContentDatabase();
        db.LoadItems(new IItemDefinition?[] { new BasicItemDefinition("") });

        var validator = new ContentValidator(db);
        bool success = validator.ValidateContent(out var errors);

        Assert.False(success);
        Assert.Contains(errors, e => e.Contains("empty id"));
    }

    [Fact]
    public void ValidateContent_NonSnakeCaseId_Fails()
    {
        var db = new ContentDatabase();
        db.LoadItems(new IItemDefinition?[] { new BasicItemDefinition("MedKitLarge") });

        var validator = new ContentValidator(db);
        bool success = validator.ValidateContent(out var errors);

        Assert.False(success);
        Assert.Contains(errors, e => e.Contains("MedKitLarge") && e.Contains("snake_case"));
    }

    [Fact]
    public void ValidateContent_UnresolvedWeaponAmmoReference_Fails()
    {
        var db = new ContentDatabase();
        db.LoadWeapons(new IWeaponDefinition?[]
        {
            new BasicWeaponDefinition { Id = "pistol", AmmoTypeId = "ammo_9mm" },
        });
        // Items registry does not contain ammo_9mm.

        var validator = new ContentValidator(db);
        bool success = validator.ValidateContent(out var errors);

        Assert.False(success);
        Assert.Contains(errors, e => e.Contains("weapon 'pistol'") && e.Contains("ammo_9mm"));
    }

    [Fact]
    public void ValidateContent_ResolvedWeaponAmmoReference_Passes()
    {
        var db = new ContentDatabase();
        db.LoadItems(new IItemDefinition?[] { new BasicItemDefinition("ammo_9mm") });
        db.LoadWeapons(new IWeaponDefinition?[]
        {
            new BasicWeaponDefinition { Id = "pistol", AmmoTypeId = "ammo_9mm" },
        });

        var validator = new ContentValidator(db);
        bool success = validator.ValidateContent(out var errors);

        Assert.True(success);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateContent_UnresolvedLootItemReference_Fails()
    {
        var db = new ContentDatabase();
        db.LoadItems(new IItemDefinition?[] { new BasicItemDefinition("medkit") });
        db.LoadLootTables(new ILootTableDefinition?[]
        {
            new BasicLootTableDefinition("field_cache", "medkit", "ghost_item"),
        });

        var validator = new ContentValidator(db);
        bool success = validator.ValidateContent(out var errors);

        Assert.False(success);
        Assert.Contains(errors, e => e.Contains("loot table 'field_cache'") && e.Contains("ghost_item"));
        Assert.DoesNotContain(errors, e => e.Contains("'medkit'"));
    }

    [Fact]
    public void ValidateContent_EmptyDatabaseAndNoDataDir_Passes()
    {
        // No projectDir at all: only registry checks run, and an empty database is valid content.
        var validator = new ContentValidator(new ContentDatabase());
        bool success = validator.ValidateContent(out var errors);

        Assert.True(success);
        Assert.Empty(errors);
    }

    // ---- Region cell scene paths (§4.2) ------------------------------------------------------------------

    [Fact]
    public void ValidateContent_RegionCellScenePathMissing_Fails()
    {
        CreateStandardStructure();
        var db = new ContentDatabase();
        db.LoadRegions(new IRegionDefinition?[]
        {
            new BasicRegionDefinition("harbor_town", "res://scenes/world/regions/harbor_town/cell_0_0.tscn"),
        });

        var validator = new ContentValidator(db, _tempProjectDir);
        bool success = validator.ValidateContent(out var errors);

        Assert.False(success);
        Assert.Contains(errors, e => e.Contains("region 'harbor_town'") && e.Contains("cell_0_0.tscn"));
    }

    [Fact]
    public void ValidateContent_RegionCellScenePathExists_Passes()
    {
        CreateStandardStructure();
        string cellDir = Path.Combine(_tempProjectDir, "scenes", "world", "regions", "harbor_town");
        Directory.CreateDirectory(cellDir);
        File.WriteAllText(Path.Combine(cellDir, "cell_0_0.tscn"), "[gd_scene format=3]\n");

        var db = new ContentDatabase();
        db.LoadRegions(new IRegionDefinition?[]
        {
            new BasicRegionDefinition("harbor_town", "res://scenes/world/regions/harbor_town/cell_0_0.tscn"),
        });

        var validator = new ContentValidator(db, _tempProjectDir);
        bool success = validator.ValidateContent(out var errors);

        Assert.True(success);
        Assert.Empty(errors);
    }

    // ---- Orphan detection (§19.1, §19.10) ----------------------------------------------------------------

    [Fact]
    public void ValidateContent_DataResourceNotInAnyIndex_IsOrphan()
    {
        CreateStandardStructure();
        WriteDataResource("items/medkit.tres", "[gd_resource type=\"Resource\" format=3]\n[resource]\nId = \"medkit\"\n");

        var validator = new ContentValidator(_tempProjectDir);
        bool success = validator.ValidateContent(out var errors);

        Assert.False(success);
        Assert.Contains(errors, e => e.Contains("Orphan content") && e.Contains("medkit.tres"));
    }

    [Fact]
    public void ValidateContent_DataResourceReferencedByIndex_IsNotOrphan()
    {
        CreateStandardStructure();
        WriteDataResource("items/medkit.tres", "[gd_resource type=\"Resource\" format=3]\n[resource]\nId = \"medkit\"\n");
        WriteIndexReferencing("item_index.tres", "res://data/items/medkit.tres");

        var validator = new ContentValidator(_tempProjectDir);
        bool success = validator.ValidateContent(out var errors);

        Assert.True(success);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateContent_IndexEntryPointingAtMissingFile_Fails()
    {
        CreateStandardStructure();
        WriteIndexReferencing("item_index.tres", "res://data/items/deleted_item.tres");

        var validator = new ContentValidator(_tempProjectDir);
        bool success = validator.ValidateContent(out var errors);

        Assert.False(success);
        Assert.Contains(errors, e => e.Contains("Index reference error") && e.Contains("deleted_item.tres"));
    }

    // Pins the B1 fix: fields merely ending in "id"/"Id" (Guid, SomeValidId…) are data, not content ids.
    // The old text-scraper flagged two files sharing such values as duplicate content ids.
    [Fact]
    public void ValidateContent_IdSuffixedFields_AreNotTreatedAsContentIds()
    {
        CreateStandardStructure();
        const string bodyA = "[gd_resource type=\"Resource\" format=3]\n[resource]\nGuid = \"abc-123\"\nSomeValidId = \"abc-123\"\n";
        const string bodyB = "[gd_resource type=\"Resource\" format=3]\n[resource]\nGuid = \"abc-123\"\n";
        WriteDataResource("items/item_a.tres", bodyA);
        WriteDataResource("items/item_b.tres", bodyB);
        WriteIndexReferencing("item_index.tres", "res://data/items/item_a.tres", "res://data/items/item_b.tres");

        var validator = new ContentValidator(_tempProjectDir);
        bool success = validator.ValidateContent(out var errors);

        Assert.True(success);
        Assert.Empty(errors);
    }

    // ---- Legacy constructor + Services (DebugConsole path) -----------------------------------------------

    [Fact]
    public void LegacyConstructor_UsesDatabaseFromServices()
    {
        CreateStandardStructure();
        var db = new ContentDatabase();
        db.LoadItems(new IItemDefinition?[] { new BasicItemDefinition("BadId") });

        Services.Register<IContentDatabase>(db);
        try
        {
            var validator = new ContentValidator(_tempProjectDir);
            bool success = validator.ValidateContent(out var errors);

            Assert.False(success);
            Assert.Contains(errors, e => e.Contains("BadId") && e.Contains("snake_case"));
        }
        finally
        {
            Services.Unregister<IContentDatabase>();
        }
    }

    // ---- Helpers -----------------------------------------------------------------------------------------

    private void CreateStandardStructure()
    {
        string[] requiredDirs = { "addons", "assets", "data", "scenes", "src", "shaders", "localization", "tests" };
        Directory.CreateDirectory(_tempProjectDir);
        foreach (var dir in requiredDirs)
        {
            Directory.CreateDirectory(Path.Combine(_tempProjectDir, dir));
        }
        Directory.CreateDirectory(Path.Combine(_tempProjectDir, "data", "indexes"));
    }

    private void WriteDataResource(string relativeDataPath, string content)
    {
        string path = Path.Combine(_tempProjectDir, "data", relativeDataPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>Writes a minimal index .tres under data/indexes/ whose ext_resource entries reference the given res:// paths.</summary>
    private void WriteIndexReferencing(string indexFileName, params string[] resPaths)
    {
        var lines = new List<string> { "[gd_resource type=\"Resource\" format=3]" };
        for (int i = 0; i < resPaths.Length; i++)
        {
            lines.Add($"[ext_resource type=\"Resource\" path=\"{resPaths[i]}\" id=\"{i + 1}\"]");
        }
        lines.Add("[resource]");

        string path = Path.Combine(_tempProjectDir, "data", "indexes", indexFileName);
        File.WriteAllText(path, string.Join("\n", lines) + "\n");
    }
}
