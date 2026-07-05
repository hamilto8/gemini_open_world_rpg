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
}
