using System;
using System.Collections.Generic;
using Meridian.Core.Save;

namespace Meridian.Core;

/// <summary>
/// Domain model storing accessibility settings (subtitles toggle, key rebindings map).
/// Decoupled from Godot for unit testing.
/// Enforces Section 23.1 and 23.3 requirements.
/// </summary>
public class AccessibilitySettings : ISaveParticipant
{
    private readonly Dictionary<string, string> _keyBindings = new(StringComparer.OrdinalIgnoreCase);

    public bool SubtitlesEnabled { get; set; } = true;
    public float TextScale { get; set; } = 1.0f; // Scale factor for dialogue and subtitles

    public IReadOnlyDictionary<string, string> KeyBindings => _keyBindings;

    public string ParticipantId => "PlayerSettings";
    public int RestoreOrder => SaveRestoreOrder.Settings;
    public Type StateType => typeof(PlayerSettingsDto);

    public void BindKey(string actionName, string keyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(actionName);
        ArgumentException.ThrowIfNullOrEmpty(keyName);
        _keyBindings[actionName] = keyName;
    }

    public string GetBoundKey(string actionName)
    {
        ArgumentException.ThrowIfNullOrEmpty(actionName);
        return _keyBindings.TryGetValue(actionName, out var key) ? key : "";
    }

    public void ClearBindings()
    {
        _keyBindings.Clear();
    }

    public object CaptureState()
    {
        return new PlayerSettingsDto(
            SubtitlesEnabled,
            TextScale,
            new Dictionary<string, string>(_keyBindings, StringComparer.OrdinalIgnoreCase));
    }

    public void RestoreState(object stateDto)
    {
        if (stateDto is not PlayerSettingsDto dto)
        {
            throw new ArgumentException("Expected player settings.", nameof(stateDto));
        }

        SubtitlesEnabled = dto.SubtitlesEnabled;
        TextScale = Math.Clamp(dto.TextScale, 0.75f, 2.0f);
        _keyBindings.Clear();
        foreach (var (action, binding) in dto.KeyBindings ?? new Dictionary<string, string>())
        {
            if (!string.IsNullOrWhiteSpace(action) && !string.IsNullOrWhiteSpace(binding))
            {
                _keyBindings[action] = binding;
            }
        }
    }
}
