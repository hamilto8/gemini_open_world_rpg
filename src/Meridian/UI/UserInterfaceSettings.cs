using System;

namespace Meridian.UI;

/// <summary>
/// Serializable player preferences owned by the UI layer. This intentionally does not participate in
/// world saves: preferences follow the player between save slots and can be loaded before a game exists.
/// </summary>
public sealed record UserInterfaceSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string Locale { get; init; } = "en";
    public float MasterVolume { get; init; } = 1f;
    public float MusicVolume { get; init; } = 0.8f;
    public float EffectsVolume { get; init; } = 1f;
    public float DialogueVolume { get; init; } = 1f;
    public bool SubtitlesEnabled { get; init; } = true;
    public float TextScale { get; init; } = 1f;
    public float HudScale { get; init; } = 1f;
    public float SafeArea { get; init; } = 1f;
    public bool HighContrast { get; init; }
    public ColorVisionMode ColorVisionMode { get; init; } = ColorVisionMode.None;
    public bool ReducedMotion { get; init; }
    public float ScreenShakeIntensity { get; init; } = 1f;
    public bool ToggleAim { get; init; }
    public bool ToggleSprint { get; init; }
    public bool InvertLookY { get; init; }
    public float MouseSensitivity { get; init; } = 1f;
    public float ControllerSensitivity { get; init; } = 1f;

    public UserInterfaceSettings Sanitize() => this with
    {
        SchemaVersion = CurrentSchemaVersion,
        Locale = string.IsNullOrWhiteSpace(Locale) ? "en" : Locale.Trim(),
        MasterVolume = Clamp01(MasterVolume),
        MusicVolume = Clamp01(MusicVolume),
        EffectsVolume = Clamp01(EffectsVolume),
        DialogueVolume = Clamp01(DialogueVolume),
        TextScale = Math.Clamp(TextScale, 0.8f, 1.5f),
        HudScale = Math.Clamp(HudScale, 0.8f, 1.4f),
        SafeArea = Math.Clamp(SafeArea, 0.8f, 1f),
        ScreenShakeIntensity = Clamp01(ScreenShakeIntensity),
        MouseSensitivity = Math.Clamp(MouseSensitivity, 0.1f, 3f),
        ControllerSensitivity = Math.Clamp(ControllerSensitivity, 0.1f, 3f),
        ColorVisionMode = Enum.IsDefined(ColorVisionMode) ? ColorVisionMode : ColorVisionMode.None,
    };

    private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
}

public enum ColorVisionMode
{
    None,
    Protanopia,
    Deuteranopia,
    Tritanopia,
}

public interface IUserInterfaceSettingsService
{
    UserInterfaceSettings Current { get; }
    event Action<UserInterfaceSettings>? Changed;
    void Update(UserInterfaceSettings settings);
    void RestoreDefaults();
}

/// <summary>Published after preferences have been persisted and applied to runtime consumers.</summary>
public readonly record struct UserInterfaceSettingsChangedEvent(UserInterfaceSettings Settings);
