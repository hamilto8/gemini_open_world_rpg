using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Meridian.Core.Registry;

namespace Meridian.Core.Validation;

/// <summary>
/// Headless content validation (§19.10). Registry-based: it checks the loaded <see cref="IContentDatabase"/>
/// for duplicate/empty ids per category, snake_case id policy (§19.9), and resolvable cross-references, then
/// sweeps the filesystem for orphaned/dangling resources. The former text-scraping id regex is gone — it
/// misparsed any <c>*id</c>-suffixed field as a content id (B1) and enforced GLOBAL id uniqueness (B2); ids
/// are unique only WITHIN a category (§19.9). Failures are errors, not warnings (§19.10).
/// </summary>
public partial class ContentValidator : IContentValidator
{
    private readonly IContentDatabase? _database;
    private readonly string? _projectDir;

    /// <summary>
    /// Legacy constructor retained for <c>DebugConsole</c>. Pulls the content database from
    /// <see cref="Services"/>; when none is registered (e.g. pre-integration) the registry checks are
    /// skipped and only the filesystem sweep runs.
    /// </summary>
    /// <param name="projectDir">Absolute path to the project root (globalized <c>res://</c>).</param>
    public ContentValidator(string projectDir)
    {
        _projectDir = projectDir;
        _database = Services.TryGet<IContentDatabase>(out var db) ? db : null;
    }

    /// <summary>
    /// Headless/test constructor. Runs registry checks against the injected <paramref name="database"/>;
    /// filesystem checks run only when <paramref name="projectDir"/> is supplied.
    /// </summary>
    public ContentValidator(IContentDatabase database, string? projectDir = null)
    {
        _database = database;
        _projectDir = projectDir;
    }

    /// <inheritdoc/>
    public bool ValidateContent(out List<string> errors)
    {
        errors = new List<string>();

        if (_database != null)
        {
            ValidateDatabase(_database, errors);
        }
        // else: no database registered. Registry checks are skipped (§19.10). This is not itself a failure,
        // and the interface exposes only an error channel, so the skip stays silent; the filesystem sweep
        // below still runs and determines the result.

        if (_projectDir != null)
        {
            ValidateFilesystem(_projectDir, errors);

            // Region cell scenes need both the database (cell paths) and the project root (disk lookup).
            if (_database != null)
            {
                ValidateRegionScenePaths(_database, _projectDir, errors);
            }
        }

        return errors.Count == 0;
    }

    // ---- Database checks -------------------------------------------------------------------------------

    private static void ValidateDatabase(IContentDatabase db, List<string> errors)
    {
        // (a) Duplicate/empty ids per category. The registries dedupe on load (first entry wins) and record
        // every duplicate/empty/null as a diagnostic; surface those as errors. Uniqueness is WITHIN a
        // category, so the same id across two categories is legal — this is the B2 fix (§19.9).
        foreach (var diagnostic in db.Diagnostics)
        {
            errors.Add($"Content id error: {diagnostic}");
        }

        // (b) Id naming policy: snake_case (§19.9).
        CheckIdNaming("Items", db.Items, errors);
        CheckIdNaming("Weapons", db.Weapons, errors);
        CheckIdNaming("LootTables", db.LootTables, errors);
        CheckIdNaming("Regions", db.Regions, errors);
        CheckIdNaming("WeatherProfiles", db.WeatherProfiles, errors);
        CheckIdNaming("MovementProfiles", db.MovementProfiles, errors);
        CheckIdNaming("HandlingProfiles", db.HandlingProfiles, errors);

        // (c) Cross-references resolve.
        // weapon -> ammo item id must exist in Items (ammo occupies inventory as an item, §6.5).
        foreach (var entry in db.Weapons.Entries)
        {
            string ammoId = entry.Value.AmmoTypeId;
            if (!string.IsNullOrEmpty(ammoId) && !db.Items.Contains(ammoId))
            {
                errors.Add($"Cross-reference error: weapon '{entry.Key}' references ammo item '{ammoId}', which is not in the Items registry.");
            }
        }

        // loot table entries -> item ids must exist in Items (§7.4).
        foreach (var entry in db.LootTables.Entries)
        {
            foreach (var itemId in entry.Value.ItemIds)
            {
                if (!string.IsNullOrEmpty(itemId) && !db.Items.Contains(itemId))
                {
                    errors.Add($"Cross-reference error: loot table '{entry.Key}' references item '{itemId}', which is not in the Items registry.");
                }
            }
        }
    }

    private static void CheckIdNaming<T>(string category, IRegistry<T> registry, List<string> errors) where T : class
    {
        foreach (var entry in registry.Entries)
        {
            if (!SnakeCaseRegex().IsMatch(entry.Key))
            {
                errors.Add($"Id naming error: '{entry.Key}' in {category} is not snake_case (lowercase letters, digits, underscores) (§19.9).");
            }
        }
    }

    // (e) Region cell scene paths must point to files that exist under the project root (§4.2, §19.10).
    private static void ValidateRegionScenePaths(IContentDatabase db, string projectDir, List<string> errors)
    {
        foreach (var entry in db.Regions.Entries)
        {
            foreach (var scenePath in entry.Value.CellScenePaths)
            {
                if (!ResPathExists(projectDir, scenePath))
                {
                    errors.Add($"Cross-reference error: region '{entry.Key}' cell scene '{scenePath}' does not exist on disk.");
                }
            }
        }
    }

    // ---- Filesystem checks -----------------------------------------------------------------------------

    private static void ValidateFilesystem(string projectDir, List<string> errors)
    {
        ValidateDirectoryStructure(projectDir, errors);

        string dataDir = Path.Combine(projectDir, "data");
        if (!Directory.Exists(dataDir))
        {
            return; // Missing data/ is reported by the structure check above; nothing more to sweep.
        }

        ValidateDanglingReferences(projectDir, dataDir, errors);
        ValidateOrphans(projectDir, dataDir, errors);
    }

    private static void ValidateDirectoryStructure(string projectDir, List<string> errors)
    {
        string[] requiredDirs =
        {
            "addons", "assets", "data", "scenes", "src", "shaders", "localization", "tests",
        };

        foreach (var dir in requiredDirs)
        {
            string fullPath = Path.Combine(projectDir, dir);
            if (!Directory.Exists(fullPath))
            {
                errors.Add($"Directory structure error: Missing '{dir}/' folder at: {fullPath}");
            }
        }
    }

    /// <summary>
    /// Scans every <c>.tres</c> under <c>data/</c> and verifies each <c>ext_resource</c> <c>res://</c> path
    /// exists on disk (no dangling references), preserving the pre-registry safety net for asset refs.
    /// </summary>
    private static void ValidateDanglingReferences(string projectDir, string dataDir, List<string> errors)
    {
        foreach (var file in Directory.EnumerateFiles(dataDir, "*.tres", SearchOption.AllDirectories))
        {
            string content;
            try
            {
                content = File.ReadAllText(file);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to read resource '{Relative(projectDir, file)}': {ex.Message}");
                continue;
            }

            if (!content.Contains("[gd_resource") && !content.Contains("[gd_scene"))
            {
                errors.Add($"Resource file error: '{Relative(projectDir, file)}' is not a valid Godot resource.");
                continue;
            }

            foreach (Match match in ExtResourcePathRegex().Matches(content))
            {
                string resPath = match.Groups["path"].Value;
                if (!ResPathExists(projectDir, resPath))
                {
                    errors.Add($"Dangling reference in '{Relative(projectDir, file)}': '{resPath}' does not exist.");
                }
            }
        }
    }

    /// <summary>
    /// (d) Orphan detection. Every <c>.tres</c> under <c>data/</c> (except <c>data/indexes/</c>) must be
    /// referenced by some index, and every path an index references must exist. Indexes reference their
    /// definition files via <c>ext_resource</c>, so this parses ONLY those paths from the index files —
    /// deliberately simple text scanning, no Godot API (§19.1, §19.10).
    /// </summary>
    private static void ValidateOrphans(string projectDir, string dataDir, List<string> errors)
    {
        string indexesDir = Path.Combine(dataDir, "indexes");

        // 1. Collect every res:// path referenced by an index file (normalized to an absolute path), and
        //    flag any index entry that points at a missing file.
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(indexesDir))
        {
            foreach (var indexFile in Directory.EnumerateFiles(indexesDir, "*.tres", SearchOption.AllDirectories))
            {
                string content;
                try
                {
                    content = File.ReadAllText(indexFile);
                }
                catch
                {
                    continue; // Unreadable index files are reported by the dangling sweep above.
                }

                foreach (Match match in ExtResourcePathRegex().Matches(content))
                {
                    string resPath = match.Groups["path"].Value;
                    if (!resPath.StartsWith("res://", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string absolute = ResToAbsolute(projectDir, resPath);
                    referenced.Add(absolute);
                    if (!File.Exists(absolute))
                    {
                        errors.Add($"Index reference error in '{Relative(projectDir, indexFile)}': '{resPath}' does not exist.");
                    }
                }
            }
        }

        // 2. Any data .tres outside data/indexes/ that no index references is an orphan.
        foreach (var file in Directory.EnumerateFiles(dataDir, "*.tres", SearchOption.AllDirectories))
        {
            if (IsUnderIndexes(file, indexesDir))
            {
                continue;
            }

            if (!referenced.Contains(Path.GetFullPath(file)))
            {
                errors.Add($"Orphan content: '{Relative(projectDir, file)}' is not referenced by any index (§19.1).");
            }
        }
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    // True when a res:// path resolves to an existing file/dir under the project root. Non-res:// schemes
    // (uid://) are resolved by Godot, not the filesystem, so they are treated as present.
    private static bool ResPathExists(string projectDir, string resPath)
    {
        if (string.IsNullOrEmpty(resPath))
        {
            return true;
        }

        if (!resPath.StartsWith("res://", StringComparison.Ordinal))
        {
            return true;
        }

        string absolute = ResToAbsolute(projectDir, resPath);
        return File.Exists(absolute) || Directory.Exists(absolute);
    }

    private static string ResToAbsolute(string projectDir, string resPath)
    {
        string relative = resPath.Substring("res://".Length);
        return Path.GetFullPath(Path.Combine(projectDir, relative.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static bool IsUnderIndexes(string file, string indexesDir)
    {
        string full = Path.GetFullPath(file);
        string idx = Path.GetFullPath(indexesDir);
        return full.StartsWith(idx + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string Relative(string projectDir, string absolutePath) => Path.GetRelativePath(projectDir, absolutePath);

    // Matches: [ext_resource type="..." path="res://..." id="..."]
    [GeneratedRegex(@"\[ext_resource[^\]]*\bpath=""(?<path>[^""]+)""")]
    private static partial Regex ExtResourcePathRegex();

    // snake_case id policy: lowercase letters, digits, and underscores only (§19.9).
    [GeneratedRegex(@"^[a-z0-9_]+$")]
    private static partial Regex SnakeCaseRegex();
}
