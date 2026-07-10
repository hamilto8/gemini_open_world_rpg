using Godot;
using System;
using System.Collections.Generic;

namespace Meridian.Data;

/// <summary>
/// Resource definition for rolling randomized loot drops from weighted entries.
/// Implements <see cref="ILootTableDefinition"/> for registry/validator decoupling (ADR-0003).
/// Enforces Section 7.4 requirements.
/// </summary>
[GlobalClass]
public partial class LootTableResource : Resource, ILootTableDefinition
{
    /// <summary>Permanent snake_case id; keys this table in the loot-table registry (§19.9).</summary>
    [Export] public string Id { get; set; } = "";

    [Export] public Godot.Collections.Array<LootEntryResource> Entries { get; set; } = new();

    string ILootTableDefinition.Id => Id;

    // Projects the referenced item ids for the validator. Skips null entries; empty ids are left in so the
    // validator surfaces them rather than silently dropping a malformed row.
    IReadOnlyList<string> ILootTableDefinition.ItemIds
    {
        get
        {
            var ids = new List<string>(Entries.Count);
            foreach (var entry in Entries)
            {
                if (entry != null)
                {
                    ids.Add(entry.ItemId);
                }
            }
            return ids;
        }
    }

    /// <summary>
    /// Rolls a single weighted item drop. Returns the item id and rolled quantity, or ("", 0) if empty.
    /// </summary>
    /// <param name="rng">
    /// Optional RNG. Defaults to <see cref="Random.Shared"/> — never a fresh time-seeded instance,
    /// which would make bursts of rolls identical and allocate per call (M7). Pass a seeded
    /// <see cref="Random"/> for deterministic tests.
    /// </param>
    public (string ItemId, int Quantity) RollDrop(Random? rng = null)
    {
        rng ??= Random.Shared;

        int totalWeight = 0;
        foreach (var entry in Entries)
        {
            if (entry != null && entry.Weight > 0)
            {
                totalWeight += entry.Weight;
            }
        }

        if (totalWeight <= 0)
        {
            return ("", 0);
        }

        int roll = rng.Next(0, totalWeight);
        int cumulative = 0;

        foreach (var entry in Entries)
        {
            if (entry == null || entry.Weight <= 0)
            {
                continue;
            }

            cumulative += entry.Weight;
            if (roll < cumulative)
            {
                int min = entry.MinQuantity;
                int max = Math.Max(entry.MinQuantity, entry.MaxQuantity);
                int quantity = min == max ? min : rng.Next(min, max + 1);
                return (entry.ItemId, quantity);
            }
        }

        return ("", 0);
    }
}
