using Godot;
using Meridian.Input;
using Meridian.UI;
using Xunit;

namespace Meridian.Tests.UI;

public sealed class UserInterfaceFrameworkTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MeridianUiTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void SettingsRoundTripAndSanitizeUnsafeValues()
    {
        string path = Path.Combine(_directory, "preferences.json");
        var store = new UserInterfaceSettingsStore(path);
        store.Save(new UserInterfaceSettings
        {
            Locale = "  fr  ",
            MasterVolume = 8f,
            TextScale = 5f,
            SafeArea = -1f,
            MouseSensitivity = 0f,
        });

        UserInterfaceSettings loaded = store.Load();

        Assert.Equal("fr", loaded.Locale);
        Assert.Equal(1f, loaded.MasterVolume);
        Assert.Equal(1.5f, loaded.TextScale);
        Assert.Equal(0.8f, loaded.SafeArea);
        Assert.Equal(0.1f, loaded.MouseSensitivity);
    }

    [Fact]
    public void CorruptSettingsFallBackToAccessibleDefaults()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "preferences.json");
        File.WriteAllText(path, "{not valid JSON");

        UserInterfaceSettings loaded = new UserInterfaceSettingsStore(path).Load();

        Assert.True(loaded.SubtitlesEnabled);
        Assert.Equal(1f, loaded.TextScale);
        Assert.Equal("en", loaded.Locale);
    }

    [Theory]
    [InlineData("DualSense Wireless Controller", ControllerFamily.PlayStation)]
    [InlineData("Xbox Wireless Controller", ControllerFamily.Xbox)]
    [InlineData("Nintendo Switch Pro Controller", ControllerFamily.Nintendo)]
    [InlineData("Unbranded USB Pad", ControllerFamily.Generic)]
    public void ControllerFamilyDetectionHasReliableFallback(string device, ControllerFamily expected)
    {
        Assert.Equal(expected, ControllerGlyphService.DetectControllerFamily(device));
    }

    [Fact]
    public void ControllerLabelsRespectFamilyConventions()
    {
        Assert.Equal("Cross", ControllerGlyphService.GetButtonLabel(ControllerFamily.PlayStation, JoyButton.A));
        Assert.Equal("B", ControllerGlyphService.GetButtonLabel(ControllerFamily.Nintendo, JoyButton.A));
        Assert.Equal("A", ControllerGlyphService.GetButtonLabel(ControllerFamily.Xbox, JoyButton.A));
    }

    [Fact]
    public void ConflictScopesAllowIntentionalVehicleReuse()
    {
        Assert.NotEqual(InputActions.ConflictScope("jump"), InputActions.ConflictScope("brake"));
        Assert.Equal(InputActions.ConflictScope("jump"), InputActions.ConflictScope("interact"));
    }
}
