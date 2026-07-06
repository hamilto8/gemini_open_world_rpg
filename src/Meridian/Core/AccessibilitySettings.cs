using System;
using System.Collections.Generic;

namespace Meridian.Core;

/// <summary>
/// Domain model storing accessibility settings (subtitles toggle, key rebindings map).
/// Decoupled from Godot for unit testing.
/// Enforces Section 23.1 and 23.3 requirements.
/// </summary>
public class AccessibilitySettings
{
    private readonly Dictionary<string, string> _keyBindings = new(StringComparer.OrdinalIgnoreCase);

    public bool SubtitlesEnabled { get; set; } = true;
    public float TextScale { get; set; } = 1.0f; // Scale factor for dialogue and subtitles

    public IReadOnlyDictionary<string, string> KeyBindings => _keyBindings;

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
}
