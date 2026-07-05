using Godot;
using System.Collections.Generic;

namespace Meridian.Data;

/// <summary>
/// Resource definition class for rolling randomized loot drops based on weights.
/// Enforces Section 7.4 requirements.
/// </summary>
[GlobalClass]
public partial class LootTableResource : Resource
{
    [Export] public Godot.Collections.Array<string> ItemIds { get; set; } = new();
    [Export] public Godot.Collections.Array<int> Weights { get; set; } = new();
    [Export] public Godot.Collections.Array<int> MinQuantities { get; set; } = new();
    [Export] public Godot.Collections.Array<int> MaxQuantities { get; set; } = new();

    /// <summary>
    /// Rolls a random item drop from this table. Returns ItemId and rolled quantity.
    /// </summary>
    public (string ItemId, int Quantity) RollDrop()
    {
        if (ItemIds.Count == 0 || ItemIds.Count != Weights.Count)
        {
            return ("", 0);
        }

        int totalWeight = 0;
        foreach (int w in Weights)
        {
            totalWeight += w;
        }

        if (totalWeight <= 0) return ("", 0);

        int roll = new Random().Next(0, totalWeight);
        int current = 0;

        for (int i = 0; i < ItemIds.Count; i++)
        {
            current += Weights[i];
            if (roll < current)
            {
                int min = i < MinQuantities.Count ? MinQuantities[i] : 1;
                int max = i < MaxQuantities.Count ? MaxQuantities[i] : 1;
                int quantity = min == max ? min : new Random().Next(min, max + 1);
                return (ItemIds[i], quantity);
            }
        }

        return ("", 0);
    }
}
