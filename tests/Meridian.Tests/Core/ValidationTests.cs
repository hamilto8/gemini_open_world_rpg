using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Meridian.Core.Validation;

namespace Meridian.Tests.Core;

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

    [Fact]
    public void ValidateContent_ShouldReturnErrorsForMissingDirectories()
    {
        // 1. Create empty folder without standard structure
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
        // 1. Setup standard directory structure
        string[] requiredDirs = {
            "addons", "assets", "data", "scenes", "src", "shaders", "localization", "tests"
        };
        Directory.CreateDirectory(_tempProjectDir);
        foreach (var dir in requiredDirs)
        {
            Directory.CreateDirectory(Path.Combine(_tempProjectDir, dir));
        }
        Directory.CreateDirectory(Path.Combine(_tempProjectDir, "data", "indexes"));

        var validator = new ContentValidator(_tempProjectDir);
        bool success = validator.ValidateContent(out var errors);

        Assert.True(success);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateContent_ShouldDetectDanglingExtResourceReference()
    {
        CreateStandardStructure();
        string weaponsDir = Path.Combine(_tempProjectDir, "data", "weapons");
        Directory.CreateDirectory(weaponsDir);
        File.WriteAllText(Path.Combine(weaponsDir, "pistol.tres"),
            "[gd_resource type=\"Resource\" load_steps=2 format=3]\n" +
            "[ext_resource type=\"Texture2D\" path=\"res://assets/missing_icon.png\" id=\"1\"]\n" +
            "[resource]\nId = \"pistol\"\n");

        var validator = new ContentValidator(_tempProjectDir);
        bool success = validator.ValidateContent(out var errors);

        Assert.False(success);
        Assert.Contains(errors, e => e.Contains("Dangling reference") && e.Contains("missing_icon.png"));
    }

    [Fact]
    public void ValidateContent_ShouldDetectDuplicateContentIds()
    {
        CreateStandardStructure();
        string weaponsDir = Path.Combine(_tempProjectDir, "data", "weapons");
        Directory.CreateDirectory(weaponsDir);
        const string body = "[gd_resource type=\"Resource\" format=3]\n[resource]\nId = \"pistol\"\n";
        File.WriteAllText(Path.Combine(weaponsDir, "pistol_a.tres"), body);
        File.WriteAllText(Path.Combine(weaponsDir, "pistol_b.tres"), body);

        var validator = new ContentValidator(_tempProjectDir);
        bool success = validator.ValidateContent(out var errors);

        Assert.False(success);
        Assert.Contains(errors, e => e.Contains("Duplicate content id 'pistol'"));
    }

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
}
