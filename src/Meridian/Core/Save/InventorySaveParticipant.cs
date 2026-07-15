using System;
using System.Collections.Generic;
using Meridian.Items;

namespace Meridian.Core.Save;

/// <summary>
/// Persists the player inventory and equipped weapon as stable content ids and plain DTOs. Keeping it
/// separate from transform/vitals state gives the module an independent restore order and migration seam.
/// </summary>
public sealed class InventorySaveParticipant : ISaveParticipant
{
    private readonly InventoryModel _inventory;
    private readonly Func<string, IItemDefinition?> _definitionResolver;
    private readonly Func<WeaponInstance?> _getEquippedWeapon;
    private readonly Action<WeaponInstance?> _setEquippedWeapon;
    private readonly Action<string>? _logger;

    public InventorySaveParticipant(
        InventoryModel inventory,
        Func<string, IItemDefinition?> definitionResolver,
        Func<WeaponInstance?> getEquippedWeapon,
        Action<WeaponInstance?> setEquippedWeapon,
        Action<string>? logger = null)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _definitionResolver = definitionResolver ?? throw new ArgumentNullException(nameof(definitionResolver));
        _getEquippedWeapon = getEquippedWeapon ?? throw new ArgumentNullException(nameof(getEquippedWeapon));
        _setEquippedWeapon = setEquippedWeapon ?? throw new ArgumentNullException(nameof(setEquippedWeapon));
        _logger = logger;
    }

    public string ParticipantId => "PlayerInventory";
    public int RestoreOrder => 70;
    public Type StateType => typeof(InventoryStateDto);

    public object CaptureState()
    {
        var items = new List<ItemInstanceDto>(_inventory.Items.Count);
        foreach (var item in _inventory.Items)
        {
            items.Add(ItemInstanceDtoMapper.ToDto(item));
        }

        WeaponInstance? equipped = _getEquippedWeapon();
        return new InventoryStateDto(
            _inventory.MaxWeight,
            items,
            equipped == null ? null : ItemInstanceDtoMapper.ToDto(equipped));
    }

    public void RestoreState(object stateDto)
    {
        if (stateDto is not InventoryStateDto dto)
        {
            return;
        }

        _inventory.Clear();

        // A saved character may legitimately be over the current capacity after balance changes. Load
        // the state losslessly, then restore the authored capacity so gameplay can apply encumbrance policy.
        float restoredMaxWeight = dto.MaxWeight;
        _inventory.MaxWeight = float.MaxValue;
        foreach (var itemDto in dto.Items ?? new List<ItemInstanceDto>())
        {
            ItemInstance item = ItemInstanceDtoMapper.FromDto(itemDto);
            RegisterDefinitionOrPlaceholder(item);
            if (!_inventory.AddItem(item))
            {
                _logger?.Invoke($"[InventorySave] Could not restore '{item.DefinitionId}' x{item.StackCount}.");
            }
        }
        _inventory.MaxWeight = restoredMaxWeight;

        WeaponInstance? equipped = dto.EquippedWeapon == null
            ? null
            : ItemInstanceDtoMapper.FromDto(dto.EquippedWeapon) as WeaponInstance;
        if (equipped != null)
        {
            RegisterDefinitionOrPlaceholder(equipped);
        }
        _setEquippedWeapon(equipped);
    }

    private void RegisterDefinitionOrPlaceholder(ItemInstance item)
    {
        IItemDefinition? definition = _definitionResolver(item.DefinitionId);
        if (definition == null)
        {
            // Unknown-content quarantine policy (§16.3): preserve the saved value instead of crashing or
            // deleting it. The zero-weight placeholder is deliberately conspicuous in logs.
            definition = new BasicItemDefinition(item.DefinitionId, Math.Max(99, item.StackCount), 0f);
            _logger?.Invoke($"[InventorySave] Unknown item '{item.DefinitionId}' restored as a placeholder.");
        }
        _inventory.RegisterDefinition(definition);
    }

}
