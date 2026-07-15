using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Meridian.Core.Save;
using System.Text.Json;

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

        public Type StateType => CapturedState.GetType();

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

    [Fact]
    public void Load_ShouldRestoreParticipant_WhoseIdDoesNotMatchAnyDtoSubstring()
    {
        // H4: a participant id like "WorldStateStore" matches none of the old substring rules
        // (Player/Flag/Time/Weather). Its state must still round-trip through real JSON dispatch.
        var saveService = new SaveService(_tempTestDir);
        var orderLog = new List<string>();

        var worldStateDto = new WorldFlagsDto(new Dictionary<string, string> { { "harbor_0_0:Chest", "True" } });
        var part = new MockSaveParticipant("WorldStateStore", 15, worldStateDto, orderLog);
        saveService.RegisterParticipant(part);

        saveService.SaveGame("slot_h4");
        Assert.True(saveService.LoadGame("slot_h4"));

        var restored = part.RestoredState as WorldFlagsDto;
        Assert.NotNull(restored);
        Assert.Equal("True", restored.Flags["harbor_0_0:Chest"]);
    }

    [Fact]
    public void Load_ShouldFallBackToBackup_WhenPrimaryIsCorrupt()
    {
        // H5: a corrupt primary should trigger the .bak fallback, not just a missing primary.
        var saveService = new SaveService(_tempTestDir);
        var orderLog = new List<string>();
        var dto = new WorldFlagsDto(new Dictionary<string, string> { { "gate", "open" } });
        saveService.RegisterParticipant(new MockSaveParticipant("WorldFlags", 10, dto, orderLog));

        // First save creates the primary; second save rotates the good primary into .bak.
        saveService.SaveGame("slot_bak");
        saveService.SaveGame("slot_bak");

        // Corrupt the primary file.
        string primary = Path.Combine(_tempTestDir, "slot_bak.json");
        File.WriteAllText(primary, "{ this is not valid json");

        Assert.True(saveService.LoadGame("slot_bak"));
        Assert.Single(orderLog); // restored from the backup
    }

    [Fact]
    public void Load_ShouldReturnFalse_WhenSlotMissing()
    {
        var saveService = new SaveService(_tempTestDir);
        Assert.False(saveService.LoadGame("no_such_slot"));
    }

    [Fact]
    public async System.Threading.Tasks.Task SaveAsync_ShouldWriteCurrentVersionAndLeaveNoTempFile()
    {
        var saveService = new SaveService(_tempTestDir);
        var part = new MockSaveParticipant(
            "WorldFlags",
            10,
            new WorldFlagsDto(new Dictionary<string, string> { ["alarm"] = "off" }),
            new List<string>());
        saveService.RegisterParticipant(part);

        await saveService.SaveGameAsync("async_slot", "Async Harbor");

        string path = Path.Combine(_tempTestDir, "async_slot.json");
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
        GameSaveData? data = JsonSerializer.Deserialize(
            File.ReadAllText(path),
            SaveJsonContext.Default.GameSaveData);
        Assert.NotNull(data);
        Assert.Equal(SaveService.CurrentSaveVersion, data.Header.SaveVersion);
        Assert.Equal(1, data.ParticipantStateVersions!["WorldFlags"]);
    }

    [Fact]
    public void ArchivedV1Fixture_ShouldMigrateRestoreAndPreserveUnavailableModule()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Saves", "v1_player_and_flags.json");
        Directory.CreateDirectory(_tempTestDir);
        File.Copy(fixture, Path.Combine(_tempTestDir, "fixture.json"));

        var player = new MockSaveParticipant(
            "PlayerState",
            SaveRestoreOrder.Possession,
            new PlayerStateDto("", 0, 0, 0, 0, 0, 0, ""),
            new List<string>());
        var flags = new MockSaveParticipant(
            "WorldFlags",
            SaveRestoreOrder.GlobalFlags,
            new WorldFlagsDto(new Dictionary<string, string>()),
            new List<string>());
        var service = new SaveService(_tempTestDir);
        service.RegisterParticipant(player);
        service.RegisterParticipant(flags);

        Assert.True(service.LoadGame("fixture"));
        var restored = Assert.IsType<PlayerStateDto>(player.RestoredState);
        Assert.Equal("harbor_town", restored.CurrentRegionId);
        Assert.Equal(73f, restored.Health);

        service.SaveGame("fixture_resaved");
        GameSaveData? resaved = JsonSerializer.Deserialize(
            File.ReadAllText(Path.Combine(_tempTestDir, "fixture_resaved.json")),
            SaveJsonContext.Default.GameSaveData);
        Assert.NotNull(resaved);
        Assert.True(resaved.ParticipantStatesJson.ContainsKey("RetiredDlcModule"));
        Assert.Equal(1, resaved.ParticipantStateVersions!["RetiredDlcModule"]);
    }

    [Fact]
    public void UnknownParticipantRejectPolicy_ShouldFailLoad()
    {
        Directory.CreateDirectory(_tempTestDir);
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Saves", "v1_player_and_flags.json");
        File.Copy(fixture, Path.Combine(_tempTestDir, "fixture.json"));
        var service = new SaveService(
            _tempTestDir,
            unknownContentPolicy: UnknownSaveContentPolicy.RejectLoad);

        Assert.False(service.LoadGame("fixture"));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("folder/slot")]
    [InlineData("..")]
    public void SlotNameTraversal_ShouldBeRejected(string slot)
    {
        var service = new SaveService(_tempTestDir);
        Assert.Throws<ArgumentException>(() => service.SaveExists(slot));
    }

    [Fact]
    public void DuplicateParticipantId_ShouldBeRejected()
    {
        var service = new SaveService(_tempTestDir);
        var first = new MockSaveParticipant(
            "Same",
            1,
            new WorldFlagsDto(new Dictionary<string, string>()),
            new List<string>());
        var second = new MockSaveParticipant(
            "Same",
            2,
            new WorldFlagsDto(new Dictionary<string, string>()),
            new List<string>());

        service.RegisterParticipant(first);
        Assert.Throws<InvalidOperationException>(() => service.RegisterParticipant(second));
    }
}
