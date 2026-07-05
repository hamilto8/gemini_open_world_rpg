using Godot;

namespace Meridian.Data;

/// <summary>
/// Resource mapping item classifications (weapon, apparel, consumable, material, ammo).
/// Enforces Section 7.1 Category requirements.
/// </summary>
[GlobalClass]
public partial class ItemCategoryResource : Resource
{
    [Export] public string Id { get; set; } = "misc";
    [Export] public string DisplayName { get; set; } = "Miscellaneous";
    [Export] public string IconPath { get; set; } = "";
    [Export] public int SortOrder { get; set; } = 100;
}
