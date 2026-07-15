using System;
using System.Collections.Generic;
using Meridian.Core;
using Meridian.Data;
using Meridian.Core.Save;

namespace Meridian.Items;

/// <summary>
/// Domain model mapping equipment slots (primary, secondary, head, chest) to item instances.
/// Updates the host StatBlock with equippable item modifiers.
/// Enforces Section 7.3 requirements.
/// </summary>
public class EquipmentModel : ISaveParticipant
{
    private readonly Dictionary<string, ItemInstance> _slots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IItemDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Modifier> _appliedModifiers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, IItemDefinition?>? _definitionResolver;
    private readonly Func<StatBlock?>? _statsAccessor;
    private readonly Action<string>? _logger;

    public IReadOnlyDictionary<string, ItemInstance> Slots => _slots;

    public string ParticipantId => "PlayerEquipment";
    public int RestoreOrder => SaveRestoreOrder.Equipment;
    public Type StateType => typeof(EquipmentStateDto);

    public EquipmentModel(
        Func<string, IItemDefinition?>? definitionResolver = null,
        Func<StatBlock?>? statsAccessor = null,
        Action<string>? logger = null)
    {
        _definitionResolver = definitionResolver;
        _statsAccessor = statsAccessor;
        _logger = logger;
    }

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

    public object CaptureState()
    {
        var slots = new Dictionary<string, ItemInstanceDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slotId, item) in _slots)
        {
            slots[slotId] = ItemInstanceDtoMapper.ToDto(item);
        }
        return new EquipmentStateDto(slots);
    }

    public void RestoreState(object stateDto)
    {
        if (stateDto is not EquipmentStateDto dto)
        {
            throw new ArgumentException("Expected equipment state.", nameof(stateDto));
        }

        StatBlock? stats = _statsAccessor?.Invoke();
        if (stats != null)
        {
            foreach (string slotId in new List<string>(_slots.Keys))
            {
                UnequipItem(slotId, stats);
            }
        }
        else
        {
            _slots.Clear();
            _appliedModifiers.Clear();
        }

        foreach (var (slotId, itemDto) in dto.Slots ?? new Dictionary<string, ItemInstanceDto>())
        {
            ItemInstance item = ItemInstanceDtoMapper.FromDto(itemDto);
            IItemDefinition? definition = _definitionResolver?.Invoke(item.DefinitionId);
            if (definition != null)
            {
                RegisterDefinition(definition);
            }

            if (stats != null && _definitions.ContainsKey(item.DefinitionId))
            {
                if (!EquipItem(slotId, item, stats))
                {
                    _logger?.Invoke($"[EquipmentSave] '{item.DefinitionId}' is no longer compatible with slot '{slotId}'; preserved without effects.");
                    _slots[slotId] = item;
                }
            }
            else
            {
                // Explicit unknown-content policy: keep the stable id and instance payload in its slot,
                // but do not invent gameplay modifiers for content absent from the current build.
                _slots[slotId] = item;
                if (definition == null)
                {
                    _logger?.Invoke($"[EquipmentSave] Unknown item '{item.DefinitionId}' preserved in slot '{slotId}'.");
                }
            }
        }
    }
}
