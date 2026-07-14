using System;
using System.Collections.Generic;
using Meridian.Core;
using Meridian.Data;

namespace Meridian.Items;

/// <summary>
/// Domain model mapping equipment slots (primary, secondary, head, chest) to item instances.
/// Updates the host StatBlock with equippable item modifiers.
/// Enforces Section 7.3 requirements.
/// </summary>
public class EquipmentModel
{
    private readonly Dictionary<string, ItemInstance> _slots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IItemDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Modifier> _appliedModifiers = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ItemInstance> Slots => _slots;

    public void RegisterDefinition(IItemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.Id] = definition;
    }

    public bool EquipItem(string slotId, ItemInstance item, StatBlock hostStats)
    {
        ArgumentException.ThrowIfNullOrEmpty(slotId);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(hostStats);

        // Verify definition exists
        if (!_definitions.TryGetValue(item.DefinitionId, out var def))
        {
            return false;
        }

        // Validate compatibility (checks if item has IEquippableBehavior matching the slot)
        IEquippableBehavior? equipBehavior = null;
        foreach (var behavior in def.Behaviors)
        {
            if (behavior is IEquippableBehavior eq && eq.SlotId.Equals(slotId, StringComparison.OrdinalIgnoreCase))
            {
                equipBehavior = eq;
                break;
            }
        }

        if (equipBehavior == null) return false;

        // Unequip current slot if occupied
        UnequipItem(slotId, hostStats);

        _slots[slotId] = item;

        // Apply stat modifiers (Section 7.3)
        if (!string.IsNullOrEmpty(equipBehavior.TargetStatId) && equipBehavior.ModifierValue != 0f)
        {
            var modifier = new Modifier(
                targetStatId: equipBehavior.TargetStatId,
                operation: ModifierOp.Add,
                value: equipBehavior.ModifierValue,
                sourceTag: $"equip_{slotId}"
            );

            _appliedModifiers[slotId] = modifier;
            hostStats.AddModifier(modifier);
        }

        return true;
    }

    public bool UnequipItem(string slotId, StatBlock hostStats)
    {
        ArgumentException.ThrowIfNullOrEmpty(slotId);
        ArgumentNullException.ThrowIfNull(hostStats);

        if (!_slots.ContainsKey(slotId))
        {
            return false;
        }

        _slots.Remove(slotId);

        // Remove applied modifier from StatBlock
        if (_appliedModifiers.TryGetValue(slotId, out var modifier))
        {
            hostStats.RemoveModifier(modifier);
            _appliedModifiers.Remove(slotId);
        }

        return true;
    }
}
