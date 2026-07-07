using System;
using System.Collections.Generic;

namespace Meridian.Items;

/// <summary>
/// Domain service implementing atomic inventory transactions (validate all, apply all, or nothing).
/// Eliminates duplication and item loss bugs during craft, upgrade, or trade operations.
/// Enforces Section 7.5 requirements.
/// </summary>
public class InventoryTransaction
{
    private readonly List<TransactionStep> _steps = new();

    public void DeductItem(InventoryModel inventory, string definitionId, int count)
    {
        _steps.Add(new DeductStep(inventory, definitionId, count));
    }

    public void AddItem(InventoryModel inventory, ItemInstance item)
    {
        _steps.Add(new AddStep(inventory, item));
    }

    public bool Execute()
    {
        // 1. Validate all steps
        foreach (var step in _steps)
        {
            if (!step.Validate())
            {
                return false; // Transaction failed validation, abort completely
            }
        }

        // 2. Apply all steps
        var applied = new List<TransactionStep>();
        try
        {
            foreach (var step in _steps)
            {
                step.Apply();
                applied.Add(step);
            }
            return true;
        }
        catch (Exception)
        {
            // 3. Rollback already applied steps in reverse order
            for (int i = applied.Count - 1; i >= 0; i--)
            {
                applied[i].Rollback();
            }
            return false;
        }
    }

    private abstract class TransactionStep
    {
        public abstract bool Validate();
        public abstract void Apply();
        public abstract void Rollback();
    }

    private class DeductStep(InventoryModel inventory, string definitionId, int count) : TransactionStep
    {
        private readonly InventoryModel _inventory = inventory;
        private readonly string _definitionId = definitionId;
        private readonly int _count = count;
        private List<ItemInstance>? _removed;

        public override bool Validate()
        {
            return _inventory.GetItemCount(_definitionId) >= _count;
        }

        public override void Apply()
        {
            if (!_inventory.RemoveItem(_definitionId, _count, out var removed))
            {
                throw new InvalidOperationException("Failed to remove item during transaction execution");
            }
            _removed = removed;
        }

        public override void Rollback()
        {
            // Re-add the exact instances that were removed, preserving unique-item payload (M17).
            if (_removed == null)
            {
                return;
            }
            foreach (var instance in _removed)
            {
                _inventory.AddItem(instance);
            }
        }
    }

    private class AddStep(InventoryModel inventory, ItemInstance item) : TransactionStep
    {
        private readonly InventoryModel _inventory = inventory;
        private readonly ItemInstance _item = item;
        private int _originalCount;

        public override bool Validate()
        {
            return _inventory.HasSpaceFor(_item.DefinitionId, _item.StackCount);
        }

        public override void Apply()
        {
            _originalCount = _item.StackCount;
            if (!_inventory.AddItem(_item))
            {
                throw new InvalidOperationException("Failed to add item during transaction execution");
            }
        }

        public override void Rollback()
        {
            _inventory.RemoveItem(_item.DefinitionId, _originalCount);
        }
    }
}
