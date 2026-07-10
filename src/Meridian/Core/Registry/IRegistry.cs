using System.Collections.Generic;

namespace Meridian.Core.Registry;

/// <summary>
/// Lookup surface for a single content category keyed by permanent snake_case id (§3.6 item 4, §19.9).
/// Ids are unique WITHIN a category; the same id may legally recur across categories (§19.9), which is
/// why each category owns its own registry instead of one global id map.
/// </summary>
/// <typeparam name="T">The category's engine-free definition interface (e.g. <c>IItemDefinition</c>).</typeparam>
public interface IRegistry<T> where T : class
{
    /// <summary>Number of registered definitions.</summary>
    int Count { get; }

    /// <summary>
    /// Registers <paramref name="definition"/> under <paramref name="id"/>. Returns false (recording a
    /// diagnostic) on an empty or duplicate id — the first entry wins and registration never throws (§19.1).
    /// </summary>
    bool Register(string id, T definition);

    /// <summary>Case-insensitive lookup; returns false and null when the id is unknown.</summary>
    bool TryGet(string id, out T? definition);

    /// <summary>Case-insensitive lookup; throws with a clear message when the id is unknown.</summary>
    T GetRequired(string id);

    /// <summary>True when an entry is registered under <paramref name="id"/> (case-insensitive).</summary>
    bool Contains(string id);

    /// <summary>Entries in deterministic insertion order (§19.1).</summary>
    IEnumerable<KeyValuePair<string, T>> Entries { get; }

    /// <summary>Registration problems collected during loading (empty/duplicate ids).</summary>
    IReadOnlyList<string> Diagnostics { get; }
}
