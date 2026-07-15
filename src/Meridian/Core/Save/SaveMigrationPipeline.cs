using System;
using System.Collections.Generic;

namespace Meridian.Core.Save;

/// <summary>
/// Validates and executes a contiguous chain of root-container migrations. It deliberately migrates
/// in memory; source files remain untouched until the player explicitly saves again.
/// </summary>
public sealed class SaveMigrationPipeline
{
    private readonly Dictionary<int, ISaveMigration> _migrations = new();

    public SaveMigrationPipeline(IEnumerable<ISaveMigration>? migrations = null)
    {
        foreach (var migration in migrations ?? DefaultMigrations())
        {
            Register(migration);
        }
    }

    public void Register(ISaveMigration migration)
    {
        ArgumentNullException.ThrowIfNull(migration);
        if (migration.TargetVersion != migration.SourceVersion + 1)
        {
            throw new ArgumentException("Save migrations must advance exactly one version.", nameof(migration));
        }
        if (!_migrations.TryAdd(migration.SourceVersion, migration))
        {
            throw new InvalidOperationException($"A migration from version {migration.SourceVersion} is already registered.");
        }
    }

    public GameSaveData MigrateTo(GameSaveData source, int targetVersion)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Header.SaveVersion > targetVersion)
        {
            throw new NotSupportedException(
                $"Save version {source.Header.SaveVersion} is newer than supported version {targetVersion}.");
        }

        GameSaveData current = source;
        while (current.Header.SaveVersion < targetVersion)
        {
            if (!_migrations.TryGetValue(current.Header.SaveVersion, out var migration))
            {
                throw new NotSupportedException(
                    $"No migration from save version {current.Header.SaveVersion} is registered.");
            }
            current = migration.Migrate(current);
            if (current.Header.SaveVersion != migration.TargetVersion)
            {
                throw new InvalidOperationException(
                    $"Migration {migration.SourceVersion}->{migration.TargetVersion} returned version {current.Header.SaveVersion}.");
            }
        }
        return current;
    }

    private static IEnumerable<ISaveMigration> DefaultMigrations()
    {
        yield return new SaveMigrationV1ToV2();
    }
}

/// <summary>Adds per-participant versions to the original modular save format.</summary>
public sealed class SaveMigrationV1ToV2 : ISaveMigration
{
    public int SourceVersion => 1;
    public int TargetVersion => 2;

    public GameSaveData Migrate(GameSaveData source)
    {
        var versions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string id in source.ParticipantStatesJson.Keys)
        {
            versions[id] = 1;
        }

        return source with
        {
            Header = source.Header with { SaveVersion = TargetVersion },
            ParticipantStateVersions = versions,
        };
    }
}
