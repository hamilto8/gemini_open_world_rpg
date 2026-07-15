using System;
using System.Collections.Generic;
using System.Globalization;
using Meridian.Items;

namespace Meridian.Core.Save;

/// <summary>Single lossless mapping between runtime item instances and save DTOs.</summary>
public static class ItemInstanceDtoMapper
{
    public static ItemInstanceDto ToDto(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in item.Payload)
        {
            payload[key] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return item is WeaponInstance weapon
            ? new ItemInstanceDto(
                item.DefinitionId,
                item.StackCount,
                payload,
                weapon.WeaponDefinitionId,
                weapon.UpgradeLevel,
                weapon.CurrentAmmo,
                new List<string>(weapon.InstalledModIds))
            : new ItemInstanceDto(
                item.DefinitionId,
                item.StackCount,
                payload,
                null,
                0,
                0,
                new List<string>());
    }

    public static ItemInstance FromDto(ItemInstanceDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ItemInstance item = string.IsNullOrEmpty(dto.WeaponDefinitionId)
            ? new ItemInstance(dto.DefinitionId, dto.StackCount)
            : new WeaponInstance(dto.DefinitionId, dto.WeaponDefinitionId, dto.StackCount)
            {
                UpgradeLevel = dto.UpgradeLevel,
                CurrentAmmo = dto.CurrentAmmo,
            };

        if (item is WeaponInstance weapon && dto.InstalledModIds != null)
        {
            weapon.InstalledModIds.AddRange(dto.InstalledModIds);
        }
        if (dto.Payload != null)
        {
            foreach (var (key, value) in dto.Payload)
            {
                item.Payload[key] = value;
            }
        }
        return item;
    }
}
