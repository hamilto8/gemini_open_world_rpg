using Godot;
using Meridian.Data.Indexes;

namespace Meridian.Core.Registry;

/// <summary>
/// Godot wrapper that registers the plain <see cref="ContentDatabase"/> in <see cref="Services"/> and
/// populates it from exported index resources at boot (§19.1). All population logic lives in
/// <see cref="ContentDatabase"/>; this Node only forwards the indexes and surfaces load diagnostics. Index
/// slots are wired in Game.tscn in a later integration pass.
/// </summary>
public partial class ContentDatabaseNode : Node
{
    /// <summary>Item index; a null slot means an empty Items category.</summary>
    [Export] public ItemIndex? Items { get; set; }

    /// <summary>Weapon index; a null slot means an empty Weapons category.</summary>
    [Export] public WeaponIndex? Weapons { get; set; }

    /// <summary>Loot-table index; a null slot means an empty LootTables category.</summary>
    [Export] public LootTableIndex? LootTables { get; set; }

    /// <summary>Region index; a null slot means an empty Regions category.</summary>
    [Export] public RegionIndex? Regions { get; set; }

    /// <summary>Weather index; a null slot means an empty WeatherProfiles category.</summary>
    [Export] public WeatherIndex? Weather { get; set; }

    /// <summary>Movement-profile index; a null slot means an empty MovementProfiles category.</summary>
    [Export] public MovementProfileIndex? MovementProfiles { get; set; }

    /// <summary>Handling-profile index; a null slot means an empty HandlingProfiles category.</summary>
    [Export] public HandlingProfileIndex? Handling { get; set; }

    private readonly ContentDatabase _database = new();

    /// <inheritdoc/>
    public override void _EnterTree()
    {
        Services.Register<IContentDatabase>(_database);
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        // Null index == empty category (§19.1); ContentDatabase tolerates null feeds.
        _database.LoadItems(Items?.Definitions);
        _database.LoadWeapons(Weapons?.Definitions);
        _database.LoadLootTables(LootTables?.Definitions);
        _database.LoadRegions(Regions?.Definitions);
        _database.LoadWeatherProfiles(Weather?.Definitions);
        _database.LoadMovementProfiles(MovementProfiles?.Definitions);
        _database.LoadHandlingProfiles(Handling?.Definitions);

        foreach (var diagnostic in _database.Diagnostics)
        {
            GD.PushWarning($"ContentDatabase: {diagnostic}");
        }
    }

    /// <inheritdoc/>
    public override void _ExitTree()
    {
        Services.Unregister<IContentDatabase>();
    }
}
