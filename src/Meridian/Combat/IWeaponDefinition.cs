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

/// <summary>
/// Basic pure C# mock implementation of IWeaponDefinition for unit testing and fallback.
/// </summary>
public class BasicWeaponDefinition : IWeaponDefinition
{
    public string Id { get; set; } = "pistol";
    public string DisplayName { get; set; } = "Pistol";
    public float BaseDamage { get; set; } = 25.0f;
    public string DamageTypeId { get; set; } = "physical";
    public float FireRate { get; set; } = 5.0f;
    public float MaxRange { get; set; } = 50.0f;
    public string AmmoTypeId { get; set; } = "ammo_9mm";
    public int MagazineSize { get; set; } = 12;
    public float ReloadTime { get; set; } = 1.5f;
}
