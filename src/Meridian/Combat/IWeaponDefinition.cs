namespace Meridian.Combat;

/// <summary>
/// Interface representing weapon properties required by the WeaponController.
/// Allows unit tests to mock weapon statistics without instantiating Godot Resource classes.
/// </summary>
public interface IWeaponDefinition
{
    string Id { get; }
    string DisplayName { get; }
    float BaseDamage { get; }
    string DamageTypeId { get; }
    float FireRate { get; }
    float MaxRange { get; }
    string AmmoTypeId { get; }
    int MagazineSize { get; }
    float ReloadTime { get; }
}
