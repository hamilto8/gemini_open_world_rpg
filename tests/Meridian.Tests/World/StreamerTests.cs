using System;
using System.Collections.Generic;
using Xunit;
using Meridian.World;

namespace Meridian.Tests.World;

public class StreamerTests
{
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
}
