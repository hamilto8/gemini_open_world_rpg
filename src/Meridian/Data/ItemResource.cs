using Godot;
using System.Collections.Generic;
using Meridian.Items;

namespace Meridian.Data;

/// <summary>
/// Database definition resource for an item. Implements IItemDefinition for domain decoupling.
/// </summary>
[GlobalClass]
public partial class ItemResource : Resource, IItemDefinition
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public string Description { get; set; } = "";
    [Export] public string IconPath { get; set; } = "";

    [Export] public ItemCategoryResource? Category { get; set; }
    [Export] public int MaxStack { get; set; } = 99;
    [Export] public float Weight { get; set; } = 0.1f;
    [Export] public int BaseValue { get; set; } = 10;
    
    [Export] public Godot.Collections.Array<ItemBehaviorResource> Behaviors { get; set; } = new();

    string IItemDefinition.Id => Id;
    int IItemDefinition.MaxStack => MaxStack;
    float IItemDefinition.Weight => Weight;
    System.Collections.Generic.IReadOnlyList<object> IItemDefinition.Behaviors => System.Linq.Enumerable.ToList(System.Linq.Enumerable.Cast<object>(Behaviors));
}
