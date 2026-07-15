using Godot;

namespace Meridian.UI;

public enum UIScreenId
{
    MainMenu,
    Pause,
    Loading,
    Settings,
    Controls,
    Inventory,
    Equipment,
    Journal,
    Map,
    Dialogue,
}

/// <summary>Content-team editable screen registration entry.</summary>
[GlobalClass]
public partial class UIScreenDefinition : Resource
{
    [Export] public UIScreenId Id { get; set; }
    [Export] public PackedScene? Scene { get; set; }
    [Export] public bool PausesGame { get; set; } = true;
    [Export] public bool CanGoBack { get; set; } = true;
}

/// <summary>Published by gameplay/content code when it wants a UI surface without owning UI nodes.</summary>
public readonly record struct UIScreenRequestedEvent(UIScreenId Screen, bool ReplaceTop = false);
