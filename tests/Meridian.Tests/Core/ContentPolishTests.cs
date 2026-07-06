using System;
using Xunit;
using Meridian.Audio;
using Meridian.Core;

namespace Meridian.Tests.Core;

public class ContentPolishTests
{
    [Fact]
    public void FootstepMaterialDetector_ShouldResolveAudioCuesByMaterialTag()
    {
        var detector = new FootstepMaterialDetector();
        detector.RegisterMaterialSfx("grass", "res://audio/step_grass.wav");
        detector.RegisterMaterialSfx("metal", "res://audio/step_metal.wav");

        Assert.Equal("res://audio/step_grass.wav", detector.GetFootstepSfx("grass"));
        Assert.Equal("res://audio/step_metal.wav", detector.GetFootstepSfx("metal"));
        Assert.Equal("", detector.GetFootstepSfx("water")); // Unregistered material
    }

    [Fact]
    public void AccessibilitySettings_ShouldTrackSubtitlesAndRemapBinds()
    {
        var settings = new AccessibilitySettings();

        // Subtitles default
        Assert.True(settings.SubtitlesEnabled);

        // Subtitles toggle
        settings.SubtitlesEnabled = false;
        Assert.False(settings.SubtitlesEnabled);

        // Key rebindings
        settings.BindKey("jump", "Space");
        settings.BindKey("fire", "MouseLeft");

        Assert.Equal("Space", settings.GetBoundKey("jump"));
        Assert.Equal("MouseLeft", settings.GetBoundKey("fire"));
    }
}
