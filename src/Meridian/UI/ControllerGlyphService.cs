using System;
using System.Collections.Generic;
using Godot;
using Meridian.Core;
using Meridian.Input;

namespace Meridian.UI;

public enum ControllerFamily
{
    Xbox,
    PlayStation,
    Nintendo,
    Generic,
}

public interface IInputGlyphService
{
    ControllerFamily ActiveControllerFamily { get; }
    string GetActionGlyph(string action);
}

/// <summary>Controller-aware text glyphs with a legible generic fallback for art-free builds.</summary>
public partial class ControllerGlyphService : Node, IInputGlyphService
{
    private static readonly Dictionary<string, JoyButton> Buttons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jump"] = JoyButton.A,
        ["brake"] = JoyButton.A,
        ["crouch"] = JoyButton.B,
        ["exit_vehicle"] = JoyButton.B,
        ["interact"] = JoyButton.X,
        ["reload"] = JoyButton.Y,
        ["menu_open"] = JoyButton.Start,
    };

    public ControllerFamily ActiveControllerFamily
    {
        get
        {
            Godot.Collections.Array<int> joypads = Godot.Input.GetConnectedJoypads();
            return joypads.Count == 0
                ? ControllerFamily.Generic
                : DetectControllerFamily(Godot.Input.GetJoyName(joypads[0]));
        }
    }

    public override void _EnterTree() => Services.Register<IInputGlyphService>(this);

    public override void _ExitTree()
    {
        if (Services.TryGet<IInputGlyphService>(out var service) && ReferenceEquals(service, this))
        {
            Services.Unregister<IInputGlyphService>();
        }
    }

    public string GetActionGlyph(string action)
    {
        if (Services.TryGet<IInputDeviceTracker>(out var tracker)
            && tracker?.ActiveDevice == InputDeviceType.Gamepad)
        {
            return Buttons.TryGetValue(action, out var button)
                ? GetButtonLabel(ActiveControllerFamily, button)
                : "Gamepad";
        }

        Key key = InputRebindStore.GetBoundKey(action);
        return key == Key.None ? "Unbound" : OS.GetKeycodeString(key);
    }

    public static ControllerFamily DetectControllerFamily(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName)) return ControllerFamily.Generic;
        string name = deviceName.ToLowerInvariant();
        if (name.Contains("playstation") || name.Contains("dualshock") || name.Contains("dualsense") || name.Contains("sony"))
            return ControllerFamily.PlayStation;
        if (name.Contains("nintendo") || name.Contains("switch") || name.Contains("joy-con"))
            return ControllerFamily.Nintendo;
        if (name.Contains("xbox") || name.Contains("xinput") || name.Contains("microsoft"))
            return ControllerFamily.Xbox;
        return ControllerFamily.Generic;
    }

    public static string GetButtonLabel(ControllerFamily family, JoyButton button) => (family, button) switch
    {
        (ControllerFamily.PlayStation, JoyButton.A) => "Cross",
        (ControllerFamily.PlayStation, JoyButton.B) => "Circle",
        (ControllerFamily.PlayStation, JoyButton.X) => "Square",
        (ControllerFamily.PlayStation, JoyButton.Y) => "Triangle",
        (ControllerFamily.PlayStation, JoyButton.Start) => "Options",
        (ControllerFamily.Nintendo, JoyButton.A) => "B",
        (ControllerFamily.Nintendo, JoyButton.B) => "A",
        (ControllerFamily.Nintendo, JoyButton.X) => "Y",
        (ControllerFamily.Nintendo, JoyButton.Y) => "X",
        (ControllerFamily.Nintendo, JoyButton.Start) => "+",
        (_, JoyButton.A) => "A",
        (_, JoyButton.B) => "B",
        (_, JoyButton.X) => "X",
        (_, JoyButton.Y) => "Y",
        (_, JoyButton.Start) => "Menu",
        _ => button.ToString(),
    };
}
