using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Meridian.Core.Validation;

/// <summary>
/// Headless content validation to catch broken references, duplicate ids, and structural problems
/// before they manifest at runtime. Enforces Section 19.10 requirements.
/// </summary>
public partial class ContentValidator : IContentValidator
{
    private readonly string _projectDir;

    public ContentValidator(string projectDir)
    {
        _projectDir = projectDir;
    }

    public bool ValidateContent(out List<string> errors)
    {
        errors = new List<string>();

        ValidateDirectoryStructure(errors);

        string dataDir = Path.Combine(_projectDir, "data");
        if (Directory.Exists(dataDir))
        {
            ValidateResourceFiles(dataDir, errors);
        }
        else
        {
            errors.Add($"Missing 'data/' folder at: {dataDir}");
        }

        return errors.Count == 0;
    }

    private void ValidateDirectoryStructure(List<string> errors)
    {
        string[] requiredDirs =
        {
            "addons", "assets", "data", "scenes", "src", "shaders", "localization", "tests",
        };

        foreach (var dir in requiredDirs)
        {
            string fullPath = Path.Combine(_projectDir, dir);
            if (!Directory.Exists(fullPath))
            {
                errors.Add($"Directory structure error: Missing '{dir}/' folder at: {fullPath}");
            }
        }
    }

    /// <summary>
    /// Scans every <c>.tres</c> under <c>data/</c>, verifying that each <c>ext_resource</c> path
    /// exists on disk (no dangling references) and that content ids are unique.
    /// </summary>
    private void ValidateResourceFiles(string dataDir, List<string> errors)
    {
        var idOwners = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(dataDir, "*.tres", SearchOption.AllDirectories))
        {
            string content;
            try
            {
                content = File.ReadAllText(file);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to read resource '{Relative(file)}': {ex.Message}");
                continue;
            }

            if (!content.Contains("[gd_resource") && !content.Contains("[gd_scene"))
            {
                errors.Add($"Resource file error: '{Relative(file)}' is not a valid Godot resource.");
                continue;
            }

            ValidateExtResourcePaths(file, content, errors);
            ValidateIds(file, content, idOwners, errors);
        }
    }

    private void ValidateExtResourcePaths(string file, string content, List<string> errors)
    {
        foreach (Match match in ExtResourcePathRegex().Matches(content))
        {
            string resPath = match.Groups["path"].Value;
            if (!resPath.StartsWith("res://", StringComparison.Ordinal))
            {
                continue; // uid:// or other schemes are resolved by Godot, not the file system
            }

            string relative = resPath.Substring("res://".Length);
            string absolute = Path.Combine(_projectDir, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolute) && !Directory.Exists(absolute))
            {
                errors.Add($"Dangling reference in '{Relative(file)}': '{resPath}' does not exist.");
            }
        }
    }

    private void ValidateIds(string file, string content, Dictionary<string, string> idOwners, List<string> errors)
    {
        foreach (Match match in ContentIdRegex().Matches(content))
        {
            string id = match.Groups["id"].Value;
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            if (idOwners.TryGetValue(id, out var existing))
            {
                errors.Add($"Duplicate content id '{id}' in '{Relative(file)}' (already declared in '{existing}').");
            }
            else
            {
                idOwners[id] = Relative(file);
            }
        }
    }

    private string Relative(string absolutePath) => Path.GetRelativePath(_projectDir, absolutePath);

    // Matches: [ext_resource type="..." path="res://..." id="..."]
    [GeneratedRegex(@"\[ext_resource[^\]]*\bpath=""(?<path>[^""]+)""")]
    private static partial Regex ExtResourcePathRegex();

    // Matches an exported content id inside the [resource] block, e.g.  Id = "pistol"  /  WeatherId = "rain"
    [GeneratedRegex(@"(?m)^\w*[Ii]d = ""(?<id>[^""]+)""")]
    private static partial Regex ContentIdRegex();
}
