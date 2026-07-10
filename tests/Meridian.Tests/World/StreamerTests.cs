using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using Meridian.Core.Save;
using Meridian.World;

namespace Meridian.Tests.World;

public class StreamerTests
{
    private static DynamicObjectRecordDto MakeRecord(string id = "dropped_medkit_1") => new(
        PersistentId: id,
        ScenePath: "res://scenes/world/shared/HealthPickup.tscn",
        PosX: 12.5f, PosY: 0.5f, PosZ: -3.25f,
        RotY: 1.57f,
        State: new Dictionary<string, string> { { "heal_amount", "40" } });

    [Fact]
    public void WorldStateStore_ShouldTrackAndRehydrateDeltas()
    {
        var store = new WorldStateStore();
        string cellKey = "wilderness_0_1";

        var deltas = new Dictionary<string, string>
        {
            { "ChestNode", "True" },
            { "DoorNode", "False" }
        };

        store.SaveCellState(cellKey, deltas);

        Assert.True(store.TryGetCellState(cellKey, out var retrieved));
        Assert.NotNull(retrieved);
        Assert.Equal("True", retrieved["ChestNode"]);
        Assert.Equal("False", retrieved["DoorNode"]);
    }

    [Fact]
    public void WorldStateStore_CaptureAndRestoreState_ShouldRoundTripSerialization()
    {
        var store = new WorldStateStore();
        string cellKey = "town_1_0";
        var deltas = new Dictionary<string, string> { { "Gate", "True" } };
        store.SaveCellState(cellKey, deltas);

        // Capture
        var stateDto = store.CaptureState();

        // Restore to a fresh store
        var freshStore = new WorldStateStore();
        freshStore.RestoreState(stateDto);

        Assert.True(freshStore.TryGetCellState(cellKey, out var retrieved));
        Assert.NotNull(retrieved);
        Assert.Equal("True", retrieved["Gate"]);
    }

    [Fact]
    public void CaptureState_MergesLiveProviderOverStaleUnloadSnapshot()
    {
        // The save-while-inside-a-loaded-cell bug: the store's last unload snapshot says the chest
        // is closed, but the live scene (via the streamer-installed provider) says it is open.
        var store = new WorldStateStore();
        store.SaveCellState("town_1_0", new Dictionary<string, string> { { "chest.open", "False" } });

        store.LiveStateProvider = () => new[]
        {
            new WorldStateStore.LiveCellState(
                "town_1_0",
                new Dictionary<string, string> { { "chest.open", "True" } },
                new List<DynamicObjectRecordDto>())
        };

        var dto = Assert.IsType<WorldStateDto>(store.CaptureState());

        Assert.Equal("True", dto.Cells["town_1_0"].ObjectDeltas["chest.open"]);
    }

    [Fact]
    public void CaptureState_RoundTripsDynamicObjectRecords()
    {
        var store = new WorldStateStore();
        store.SaveCellDynamicObjects("town_1_0", new List<DynamicObjectRecordDto> { MakeRecord() });

        var restored = new WorldStateStore();
        restored.RestoreState(store.CaptureState());

        var record = Assert.Single(restored.GetCellDynamicObjects("town_1_0"));
        Assert.Equal("dropped_medkit_1", record.PersistentId);
        Assert.Equal("res://scenes/world/shared/HealthPickup.tscn", record.ScenePath);
        Assert.Equal(12.5f, record.PosX, 4);
        Assert.Equal(-3.25f, record.PosZ, 4);
        Assert.Equal(1.57f, record.RotY, 4);
        Assert.Equal("40", record.State["heal_amount"]);
    }

    [Fact]
    public void WorldStateDto_RoundTripsThroughSaveJsonContext()
    {
        // The DTO must survive the same source-generated serialization path SaveService uses.
        var store = new WorldStateStore();
        store.SaveCellState("town_1_0", new Dictionary<string, string> { { "gate.open", "True" } });
        store.SaveCellDynamicObjects("town_1_0", new List<DynamicObjectRecordDto> { MakeRecord() });

        string json = JsonSerializer.Serialize(store.CaptureState(), typeof(WorldStateDto), SaveJsonContext.Default);
        object? parsed = JsonSerializer.Deserialize(json, typeof(WorldStateDto), SaveJsonContext.Default);

        var restored = new WorldStateStore();
        restored.RestoreState(parsed!);

        Assert.True(restored.TryGetCellState("town_1_0", out var deltas));
        Assert.Equal("True", deltas!["gate.open"]);
        Assert.Single(restored.GetCellDynamicObjects("town_1_0"));
    }

    [Fact]
    public void RestoreState_RaisesStateRestored_OnlyForValidDto()
    {
        var store = new WorldStateStore();
        int raised = 0;
        store.StateRestored += () => raised++;

        // Wrong-shaped payload (e.g. a legacy module deserialized to another type): no event, no throw.
        store.RestoreState(new object());
        Assert.Equal(0, raised);

        store.RestoreState(new WorldStateDto(new Dictionary<string, CellStateDto>()));
        Assert.Equal(1, raised);
    }

    [Fact]
    public void RestoreState_ToleratesNullCellsAndNullMembers()
    {
        // A legacy (pre-WorldStateDto) save deserializes with Cells == null; partially-written cells
        // may carry null members. Loads never crash on content drift (doc §16.3).
        var store = new WorldStateStore();
        store.SaveCellState("stale", new Dictionary<string, string> { { "x", "1" } });

        store.RestoreState(new WorldStateDto(null!));
        Assert.False(store.TryGetCellState("stale", out _));

        store.RestoreState(new WorldStateDto(new Dictionary<string, CellStateDto>
        {
            { "town_1_0", new CellStateDto(null!, null!) }
        }));
        Assert.True(store.TryGetCellState("town_1_0", out var deltas));
        Assert.Empty(deltas!);
        Assert.Empty(store.GetCellDynamicObjects("town_1_0"));
    }
}
