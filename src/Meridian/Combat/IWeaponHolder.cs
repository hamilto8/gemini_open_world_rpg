using Meridian.Items;

namespace Meridian.Combat;

/// <summary>
/// Implemented by an avatar that can hold and fire a weapon. Lets pickups equip a weapon without
/// depending on the concrete avatar type (Section 5.2 EquipmentHolder).
/// </summary>
public interface IWeaponHolder
{
    /// <summary>Equips the given weapon instance/definition, wiring it to the holder's firing controller.</summary>
    void EquipWeapon(WeaponInstance instance, IWeaponDefinition definition);
}

/// <summary>Published when the player equips a weapon (HUD/ammo widgets react).</summary>
public readonly record struct WeaponEquippedEvent(string WeaponDefinitionId, int CurrentAmmo, int MagazineSize);

/// <summary>Published when a weapon's ammo changes (fired/reloaded) so the HUD can update.</summary>
public readonly record struct WeaponAmmoChangedEvent(int CurrentAmmo, int MagazineSize, int Reserve);
