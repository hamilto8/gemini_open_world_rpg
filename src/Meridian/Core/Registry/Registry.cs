using System;
using System.Collections.Generic;

namespace Meridian.Core.Registry;

/// <summary>
/// Insertion-ordered, id-keyed store for one content category. The first entry wins on a duplicate id and
/// registration never throws at boot (§19.1) — problems are collected as diagnostics for the validator to
/// report (§19.10). Ids are compared case-insensitively (<see cref="StringComparer.OrdinalIgnoreCase"/>),
/// matching InventoryModel; canonical ids are snake_case (§19.9).
/// </summary>
/// <typeparam name="T">The category's engine-free definition interface.</typeparam>
public sealed class Registry<T> : IRegistry<T> where T : class
{
    private readonly Dictionary<string, T> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<KeyValuePair<string, T>> _ordered = new();
    private readonly List<string> _diagnostics = new();

    /// <inheritdoc/>
    public int Count => _ordered.Count;

    /// <inheritdoc/>
    public IReadOnlyList<string> Diagnostics => _diagnostics;

    /// <inheritdoc/>
    public IEnumerable<KeyValuePair<string, T>> Entries => _ordered;

    /// <inheritdoc/>
    public bool Register(string id, T definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(id))
        {
            _diagnostics.Add($"Rejected a {typeof(T).Name} entry with an empty id.");
            return false;
        }

        if (_byId.ContainsKey(id))
        {
            // Ids are unique within a category (§19.9). Keep the first entry; ignore later duplicates.
            _diagnostics.Add($"Duplicate id '{id}' ignored; the first registered entry is kept.");
            return false;
        }

        _byId.Add(id, definition);
        _ordered.Add(new KeyValuePair<string, T>(id, definition));
        return true;
    }

    /// <inheritdoc/>
    public bool TryGet(string id, out T? definition)
    {
        if (id != null && _byId.TryGetValue(id, out var value))
        {
            definition = value;
            return true;
        }

        definition = null;
        return false;
    }

    /// <inheritdoc/>
    public T GetRequired(string id)
    {
        if (id != null && _byId.TryGetValue(id, out var value))
        {
            return value;
        }

        throw new KeyNotFoundException(
            $"No {typeof(T).Name} is registered under id '{id}'. Ids are permanent (§19.9); " +
            "verify the index entry and any cross-reference pointing at it.");
    }

    /// <inheritdoc/>
    public bool Contains(string id) => id != null && _byId.ContainsKey(id);
}
