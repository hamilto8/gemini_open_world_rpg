using Godot;

namespace Meridian.Data;

/// <summary>
/// Data-driven definition Resource for a Weapon. Implements IWeaponDefinition for domain decoupling.
/// </summary>
[GlobalClass]
public partial class WeaponResource : Resource, Meridian.Combat.IWeaponDefinition
{
    [ExportGroup("Identity")]
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public string Description { get; set; } = "";

    [ExportGroup("Ballistics")]
    [Export] public float BaseDamage { get; set; } = 25.0f;
    [Export] public string DamageTypeId { get; set; } = "physical";
    [Export] public float FireRate { get; set; } = 5.0f;
    [Export] public float MaxRange { get; set; } = 50.0f;

    [ExportGroup("Ammo")]
    [Export] public string AmmoTypeId { get; set; } = "ammo_9mm";
    [Export] public int MagazineSize { get; set; } = 12;
    [Export] public float ReloadTime { get; set; } = 1.8f;
}
