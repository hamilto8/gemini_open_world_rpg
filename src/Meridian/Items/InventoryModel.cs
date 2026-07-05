using System;
using System.Collections.Generic;
using System.Linq;
using Meridian.Core;
using Meridian.Data;

namespace Meridian.Items;

/// <summary>
/// Domain model for an inventory container (e.g. player inventory, storage, vendor stock).
/// Completely decoupled from Godot nodes for headless unit testing.
/// Enforces Section 7.2 requirements.
/// </summary>
public class InventoryModel
{
    private readonly List<ItemInstance> _items = new();
    private readonly Dictionary<string, IItemDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

    public float MaxWeight { get; set; } = 50.0f;
    public event Action? InventoryChanged;

    public IReadOnlyList<ItemInstance> Items => _items;

    public void RegisterDefinition(IItemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.Id] = definition;
    }

    public float CalculateTotalWeight()
    {
        float total = 0f;
        foreach (var item in _items)
        {
            if (_definitions.TryGetValue(item.DefinitionId, out var def))
            {
                total += def.Weight * item.StackCount;
            }
        }
        return total;
    }

    public int GetItemCount(string definitionId)
    {
        return _items.Where(i => i.DefinitionId.Equals(definitionId, StringComparison.OrdinalIgnoreCase))
                     .Sum(i => i.StackCount);
    }

    public bool HasSpaceFor(string definitionId, int count)
    {
        if (!_definitions.TryGetValue(definitionId, out var def)) return false;
        
        // Simple weight-based encumbrance gating (Section 7.2 capacity policy)
        float itemWeight = def.Weight * count;
        if (CalculateTotalWeight() + itemWeight > MaxWeight)
        {
            return false;
        }
        return true;
    }

    public bool AddItem(ItemInstance itemInstance)
    {
        ArgumentNullException.ThrowIfNull(itemInstance);

        if (!_definitions.TryGetValue(itemInstance.DefinitionId, out var def))
        {
            // Register auto-stub definition if unregistered to ease testing
            def = new BasicItemDefinition(itemInstance.DefinitionId);
            _definitions[def.Id] = def;
        }

        if (!HasSpaceFor(itemInstance.DefinitionId, itemInstance.StackCount))
        {
            return false;
        }

        // Try stacking
        if (def.MaxStack > 1)
        {
            var existing = _items.FirstOrDefault(i => i.DefinitionId.Equals(itemInstance.DefinitionId, StringComparison.OrdinalIgnoreCase) && i.StackCount < def.MaxStack);
            if (existing != null)
            {
                int canAdd = def.MaxStack - existing.StackCount;
                int adding = Math.Min(canAdd, itemInstance.StackCount);
                existing.StackCount += adding;
                itemInstance.StackCount -= adding;

                if (itemInstance.StackCount > 0)
                {
                    // Recurse to add remainder
                    return AddItem(itemInstance);
                }
                
                TriggerChanged();
                return true;
            }
        }

        _items.Add(itemInstance);
        TriggerChanged();
        return true;
    }

    public bool RemoveItem(string definitionId, int count)
    {
        if (GetItemCount(definitionId) < count)
        {
            return false;
        }

        int remaining = count;
        for (int i = _items.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var item = _items[i];
            if (item.DefinitionId.Equals(definitionId, StringComparison.OrdinalIgnoreCase))
            {
                if (item.StackCount <= remaining)
                {
                    remaining -= item.StackCount;
                    _items.RemoveAt(i);
                }
                else
                {
                    item.StackCount -= remaining;
                    remaining = 0;
                }
            }
        }

        TriggerChanged();
        return true;
    }

    private void TriggerChanged()
    {
        InventoryChanged?.Invoke();
        
        // Publish event to EventBus (Section 7.2 query API)
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new InventoryChangedEvent());
        }
    }
}

/// <summary>
/// Event broadcast when inventory changes.
/// </summary>
public record struct InventoryChangedEvent;
