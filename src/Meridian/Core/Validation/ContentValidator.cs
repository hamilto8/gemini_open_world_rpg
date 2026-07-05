using System;
using System.Collections.Generic;
using System.IO;

namespace Meridian.Core.Validation;

/// <summary>
/// Headless content validation check to prevent runtime typos/broken references.
/// Enforces Section 19.10 requirements.
/// </summary>
public class ContentValidator : IContentValidator
{
    private readonly string _projectDir;

    public ContentValidator(string projectDir)
    {
        _projectDir = projectDir;
    }

    public bool ValidateContent(out List<string> errors)
    {
        errors = new List<string>();

        // 1. Verify standard directories are intact
        string[] requiredDirs = {
            "addons",
            "assets",
            "data",
            "scenes",
            "src",
            "shaders",
            "localization",
            "tests"
        };

        foreach (var dir in requiredDirs)
        {
            string fullPath = Path.Combine(_projectDir, dir);
            if (!Directory.Exists(fullPath))
            {
                errors.Add($"Directory structure error: Missing '{dir}/' folder at: {fullPath}");
            }
        }

        // 2. Validate index resources under data/indexes (if any exist)
        string indexDir = Path.Combine(_projectDir, "data", "indexes");
        if (Directory.Exists(indexDir))
        {
            var indexFiles = Directory.GetFiles(indexDir, "*.tres");
            foreach (var file in indexFiles)
            {
                ValidateIndexFile(file, errors);
            }
        }
        else
        {
            errors.Add($"Missing data directory path: {indexDir}");
        }

        return errors.Count == 0;
    }

    private void ValidateIndexFile(string filePath, List<string> errors)
    {
        try
        {
            string content = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(content))
            {
                errors.Add($"Index file error: '{Path.GetFileName(filePath)}' is empty.");
                return;
            }

            // Basic parsing of Godot .tres file format
            if (!content.Contains("[gd_resource") && !content.Contains("[resource"))
            {
                errors.Add($"Index file error: '{Path.GetFileName(filePath)}' is not a valid Godot resource.");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to read index file '{Path.GetFileName(filePath)}': {ex.Message}");
        }
    }
}
