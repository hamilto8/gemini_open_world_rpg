using System.Collections.Generic;

namespace Meridian.Data;

/// <summary>
/// Engine-free view of a loot table for the registry and validator (ADR-0003): its permanent id plus the
/// item ids its entries reference, so the validator can prove every loot-&gt;item cross-reference resolves
/// (§7.4, §19.10) without instantiating the Godot Resource.
/// </summary>
public interface ILootTableDefinition
{
    /// <summary>Permanent snake_case id, unique within the loot-table category (§19.9).</summary>
    string Id { get; }

    /// <summary>Item ids referenced by this table's entries; validated against the Items registry.</summary>
    IReadOnlyList<string> ItemIds { get; }
}
