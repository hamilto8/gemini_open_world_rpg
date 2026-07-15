using System;
using System.Collections.Generic;
using Meridian.Core;
using Meridian.Core.Save;
using Meridian.Items;
using Meridian.Factions;
using Meridian.World;
using Xunit;

namespace Meridian.Tests.Core;

public sealed class SaveParticipantTests
{
    [Fact]
    public void ProgressionRestore_ShouldRoundTripAndReapplyPerks()
    {
        var profile = new BasicProgressionProfile { BaseXpRequired = 100, MaxLevel = 10 };
        var source = new ProgressionManager(profile);
        var sourceStats = new StatBlock();
        source.AddXp(100);
        Assert.True(source.UnlockPerk("fast_reload", sourceStats));

        var restoredStats = new StatBlock();
        var restored = new ProgressionManager(profile, () => restoredStats);
        restored.RestoreState(source.CaptureState());

        Assert.Equal(2, restored.Level);
        Assert.Equal(1, restored.SkillPoints);
        Assert.Contains("fast_reload", restored.UnlockedPerks);
        Assert.Equal(1.15f, restoredStats.GetStat("reload_speed"));
    }

    [Fact]
    public void EquipmentRestore_ShouldPreserveUnknownItemWithoutInventingModifier()
    {
        var equipment = new EquipmentModel();
        var dto = new EquipmentStateDto(new Dictionary<string, ItemInstanceDto>
        {
            ["chest"] = new(
                "removed_dlc_armor",
                1,
                new Dictionary<string, string> { ["dye"] = "red" },
                null,
                0,
                0,
                new List<string>()),
        });

        equipment.RestoreState(dto);

        Assert.Equal("removed_dlc_armor", equipment.Slots["chest"].DefinitionId);
        Assert.Equal("red", equipment.Slots["chest"].Payload["dye"]);
        var roundTrip = Assert.IsType<EquipmentStateDto>(equipment.CaptureState());
        Assert.Equal("removed_dlc_armor", roundTrip.Slots["chest"].DefinitionId);
    }

    [Fact]
    public void DiscoveryRestore_ShouldApplyIdsRegisteredAfterLoad()
    {
        var network = new FastTravelNetwork();
        network.RestoreState(new DiscoveriesStateDto(new List<string> { "late_terminal" }));

        network.RegisterNode("late_terminal", "Late Terminal", Godot.Vector3.Zero);

        Assert.True(network.CanTravelTo("late_terminal"));
        var saved = Assert.IsType<DiscoveriesStateDto>(network.CaptureState());
        Assert.Contains("late_terminal", saved.DiscoveredIds);
    }

    [Fact]
    public void SettingsFactionAndQuickSlots_ShouldRoundTrip()
    {
        var settings = new AccessibilitySettings
        {
            SubtitlesEnabled = false,
            TextScale = 1.4f,
        };
        settings.BindKey("interact", "F");
        var settingsCopy = new AccessibilitySettings();
        settingsCopy.RestoreState(settings.CaptureState());
        Assert.False(settingsCopy.SubtitlesEnabled);
        Assert.Equal(1.4f, settingsCopy.TextScale);
        Assert.Equal("F", settingsCopy.GetBoundKey("interact"));

        var definitions = new[] { new SavedFaction("harbor_guild", -100, 100, 0) };
        var factions = new FactionReputationService(definitions);
        factions.ModifyReputation("harbor_guild", 25);
        var factionCopy = new FactionReputationService(definitions);
        factionCopy.RestoreState(factions.CaptureState());
        Assert.Equal(25, factionCopy.GetReputation("harbor_guild"));

        var quickSlots = new QuickSlotModel();
        quickSlots.Bind(0, "health_kit");
        var quickSlotCopy = new QuickSlotModel();
        quickSlotCopy.RestoreState(quickSlots.CaptureState());
        Assert.Equal("health_kit", quickSlotCopy.Bindings[0]);
    }

    [Fact]
    public void VehicleState_ShouldWaitForStreamedVehicleRegistration()
    {
        var service = new VehiclePersistenceService(() => "harbor", () => null);
        var saved = new VehicleStateDto(
            "buggy-01",
            "buggy",
            "harbor",
            1, 2, 3,
            0.5f,
            40,
            75,
            false,
            new Dictionary<string, string>());
        service.RestoreState(new VehicleFleetStateDto(new List<VehicleStateDto> { saved }));
        var vehicle = new FakeVehicle("buggy-01");

        service.Register(vehicle);

        Assert.Same(saved, vehicle.Restored);
    }

    private sealed class FakeVehicle : IPersistentVehicle
    {
        public FakeVehicle(string id) => PersistentVehicleId = id;

        public string PersistentVehicleId { get; }
        public string VehicleDefinitionId => "buggy";
        public VehicleStateDto? Restored { get; private set; }

        public VehicleStateDto CaptureVehicleState(string currentRegionId, bool isPlayerPossessed)
        {
            return Restored ?? new VehicleStateDto(
                PersistentVehicleId,
                VehicleDefinitionId,
                currentRegionId,
                0, 0, 0, 0, 0, 100,
                isPlayerPossessed,
                new Dictionary<string, string>());
        }

        public void RestoreVehicleState(VehicleStateDto state) => Restored = state;
    }

    private sealed record SavedFaction(
        string Id,
        int MinimumReputation,
        int MaximumReputation,
        int StartingReputation) : IFactionDefinition
    {
        public string DisplayName => Id;
    }
}
