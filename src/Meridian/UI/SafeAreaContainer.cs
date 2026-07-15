using System;
using Godot;
using Meridian.Core;

namespace Meridian.UI;

/// <summary>Applies player-calibrated overscan margins and UI scale to every authored screen.</summary>
public partial class SafeAreaContainer : MarginContainer
{
    [Export(PropertyHint.Range, "0,128,1")] public int MinimumMargin { get; set; } = 24;
    [Export(PropertyHint.Range, "720,2560,1")] public int ReferenceWidth { get; set; } = 1920;

    private IDisposable? _subscription;

    public override void _Ready()
    {
        Resized += Refresh;
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            _subscription = eventBus.Subscribe<UserInterfaceSettingsChangedEvent>(_ => Refresh());
        }
        Refresh();
    }

    public override void _ExitTree() => _subscription?.Dispose();

    private void Refresh()
    {
        UserInterfaceSettings settings = Services.TryGet<IUserInterfaceSettingsService>(out var service) && service != null
            ? service.Current
            : new UserInterfaceSettings();
        float missingSafeArea = 1f - settings.SafeArea;
        int horizontal = Math.Max(MinimumMargin, Mathf.RoundToInt(Size.X * missingSafeArea * 0.5f));
        int vertical = Math.Max(MinimumMargin, Mathf.RoundToInt(Size.Y * missingSafeArea * 0.5f));
        AddThemeConstantOverride("margin_left", horizontal);
        AddThemeConstantOverride("margin_right", horizontal);
        AddThemeConstantOverride("margin_top", vertical);
        AddThemeConstantOverride("margin_bottom", vertical);

        float viewportScale = Mathf.Clamp(Size.X / ReferenceWidth, 0.75f, 1.25f);
        Theme?.SetDefaultFontSize(Mathf.RoundToInt(18f * settings.TextScale * viewportScale));
    }
}
