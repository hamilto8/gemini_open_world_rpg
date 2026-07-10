using System;
using System.Collections.Generic;
using Meridian.Core.Save;

namespace Meridian.Core;

/// <summary>
/// Read/write access to the world-flags store — the game's "consequence memory" (§3.6, §9.5, §16.1).
/// Registered in the <see cref="Services"/> locator so conditions, actions, dialogue, and quests all
/// read and write the same flag namespace.
/// </summary>
public interface IWorldFlags
{
    /// <summary>Returns a flag's boolean value; absent flags read as <c>false</c>.</summary>
    bool GetFlag(string id);

    /// <summary>Sets a flag's boolean value, raising <see cref="FlagChanged"/> when it changes.</summary>
    void SetFlag(string id, bool value);

    /// <summary>Attempts to read a flag's raw string value (flags are stored string→string).</summary>
    bool TryGetValue(string id, out string value);

    /// <summary>Sets a flag's raw string value, raising <see cref="FlagChanged"/> when it changes.</summary>
    void SetValue(string id, string value);

    /// <summary>Raised with the flag id whenever a flag's stored value changes.</summary>
    event Action<string>? FlagChanged;

    /// <summary>Clears every flag.</summary>
    void Clear();
}

/// <summary>
/// Plain-C# world-flags store. Flags are held as string→string (matching <see cref="WorldFlagsDto"/>),
/// so the same store backs boolean consequence flags and arbitrary string state. Participates in
/// save/restore with <c>RestoreOrder = 10</c> — the earliest slot (per the <see cref="ISaveParticipant"/>
/// doc), because later modules read flags during their own restore (§16.2).
/// Fully headless-testable: no Godot references.
/// </summary>
public sealed class WorldFlagsService : IWorldFlags, ISaveParticipant
{
    private readonly Dictionary<string, string> _flags = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public event Action<string>? FlagChanged;

    /// <inheritdoc />
    public string ParticipantId => "WorldFlags";

    /// <inheritdoc />
    public int RestoreOrder => 10;

    /// <inheritdoc />
    public Type StateType => typeof(WorldFlagsDto);

    /// <inheritdoc />
    public bool GetFlag(string id)
    {
        if (string.IsNullOrEmpty(id) || !_flags.TryGetValue(id, out var raw))
        {
            return false;
        }

        // A flag reads true only if it holds an explicit truthy token; arbitrary string-valued flags
        // (set via SetValue) therefore read as false through the boolean accessor, keeping the two uses
        // from silently colliding.
        return bool.TryParse(raw, out bool parsed) ? parsed : string.Equals(raw, "1", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public void SetFlag(string id, bool value) => SetValue(id, value ? "true" : "false");

    /// <inheritdoc />
    public bool TryGetValue(string id, out string value)
    {
        if (!string.IsNullOrEmpty(id) && _flags.TryGetValue(id, out var raw))
        {
            value = raw;
            return true;
        }

        value = string.Empty;
        return false;
    }

    /// <inheritdoc />
    public void SetValue(string id, string value)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        value ??= string.Empty;

        // Only fire the change event when the stored value actually moves, so subscribers aren't
        // spammed by idempotent writes.
        if (_flags.TryGetValue(id, out var existing) && string.Equals(existing, value, StringComparison.Ordinal))
        {
            return;
        }

        _flags[id] = value;
        FlagChanged?.Invoke(id);
    }

    /// <inheritdoc />
    public void Clear() => _flags.Clear();

    /// <inheritdoc />
    public object CaptureState()
    {
        // Copy so later mutation of the live store can't retroactively alter a captured DTO.
        return new WorldFlagsDto(new Dictionary<string, string>(_flags, StringComparer.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public void RestoreState(object stateDto)
    {
        _flags.Clear();

        // Null-tolerant: a missing module or a DTO with a null map restores to an empty flag set rather
        // than throwing (§16.3 — loads never crash on content drift). Bulk restore does not fan out
        // per-flag change events (that would be spurious noise during load).
        if (stateDto is not WorldFlagsDto dto || dto.Flags is null)
        {
            return;
        }

        foreach (var kv in dto.Flags)
        {
            if (!string.IsNullOrEmpty(kv.Key))
            {
                _flags[kv.Key] = kv.Value ?? string.Empty;
            }
        }
    }
}
