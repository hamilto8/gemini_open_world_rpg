using Godot;
using System.Collections.Generic;
using Meridian.Items;

namespace Meridian.Data;

/// <summary>
/// Abstract base class for behaviors that can be attached to items as data.
/// Enforces Section 7.1 behavior composition.
/// </summary>
public abstract partial class ItemBehaviorResource : Resource
{
}

/// <summary>
/// Behavior indicating that an item is equippable in a slot and grants modifiers.
/// </summary>
[GlobalClass]
public partial class EquippableBehavior : ItemBehaviorResource, IEquippableBehavior
{
    [Export] public string SlotId { get; set; } = "primary";

    // List of stat modifiers granted when equipped (e.g. +10 max_health, +5 armor)
    [Export] public string TargetStatId { get; set; } = "";
    [Export] public float ModifierValue { get; set; } = 0f;

    string IEquippableBehavior.SlotId => SlotId;
    string IEquippableBehavior.TargetStatId => TargetStatId;
    float IEquippableBehavior.ModifierValue => ModifierValue;
}

/// <summary>
/// Behavior indicating that an item is usable and triggers an effect.
/// </summary>
[GlobalClass]
public partial class UsableBehavior : ItemBehaviorResource
{
    [Export] public string ActionName { get; set; } = "";
    [Export] public int MaxCharges { get; set; } = 1;
    [Export] public float CooldownSeconds { get; set; } = 1.0f;
    [Export] public bool ConsumeOnUse { get; set; } = true;
}
