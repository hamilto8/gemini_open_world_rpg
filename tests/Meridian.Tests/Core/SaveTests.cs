using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Meridian.Core.Save;

namespace Meridian.Tests.Core;

public class SaveTests : IDisposable
{
    private readonly string _tempTestDir;

    public SaveTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "MeridianSaveTests_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempTestDir))
        {
            Directory.Delete(_tempTestDir, true);
        }
    }

    private class MockSaveParticipant : ISaveParticipant
    {
        public string ParticipantId { get; }
        public int RestoreOrder { get; }
        public object CapturedState { get; set; }
        public object? RestoredState { get; private set; }
        public List<string> OrderLog { get; }

        public MockSaveParticipant(string id, int order, object defaultState, List<string> orderLog)
        {
            ParticipantId = id;
            RestoreOrder = order;
            CapturedState = defaultState;
            OrderLog = orderLog;
        }

        public object CaptureState() => CapturedState;

        public void RestoreState(object stateDto)
        {
            RestoredState = stateDto;
            OrderLog.Add(ParticipantId);
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldRoundTripParticipantStatesInCorrectOrder()
    {
        var saveService = new SaveService(_tempTestDir);
        var orderLog = new List<string>();

        var playerDto = new PlayerStateDto("Region1", 10f, 20f, 30f, 45f, 100f, 80f, "guid-123");
        var playerPart = new MockSaveParticipant("PlayerState", 100, playerDto, orderLog);

        var worldFlagsDto = new WorldFlagsDto(new Dictionary<string, string> { { "gate_opened", "true" } });
        var flagsPart = new MockSaveParticipant("WorldFlags", 10, worldFlagsDto, orderLog);

        saveService.RegisterParticipant(playerPart);
        saveService.RegisterParticipant(flagsPart);

        Assert.False(saveService.SaveExists("slot1"));
        saveService.SaveGame("slot1", "Harbor Town");
        Assert.True(saveService.SaveExists("slot1"));

        // Load it back
        bool loadSuccess = saveService.LoadGame("slot1");
        Assert.True(loadSuccess);

        // Verify restoration order (WorldFlags should be restored before PlayerState)
        Assert.Equal(2, orderLog.Count);
        Assert.Equal("WorldFlags", orderLog[0]);
        Assert.Equal("PlayerState", orderLog[1]);

        // Verify restored DTOs
        var restoredFlags = flagsPart.RestoredState as WorldFlagsDto;
        Assert.NotNull(restoredFlags);
        Assert.Equal("true", restoredFlags.Flags["gate_opened"]);

        var restoredPlayer = playerPart.RestoredState as PlayerStateDto;
        Assert.NotNull(restoredPlayer);
        Assert.Equal("Region1", restoredPlayer.CurrentRegionId);
        Assert.Equal(10f, restoredPlayer.PositionX);
        Assert.Equal("guid-123", restoredPlayer.PossessedGuid);
    }
}
