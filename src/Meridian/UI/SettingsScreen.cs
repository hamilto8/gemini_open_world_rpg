using Godot;
using Meridian.Core;
using Meridian.Input;

namespace Meridian.UI;

/// <summary>Connects the authored settings form to the persistent preference service.</summary>
public partial class SettingsScreen : UIScreen
{
    private IUserInterfaceSettingsService? _settings;
    private CheckButton? _subtitles;
    private CheckButton? _highContrast;
    private CheckButton? _reducedMotion;
    private CheckButton? _invertY;
    private HSlider? _textScale;
    private HSlider? _hudScale;
    private HSlider? _safeArea;
    private HSlider? _masterVolume;
    private Label? _status;

    public override void _Ready()
    {
        base._Ready();
        Services.TryGet(out _settings);
        _subtitles = GetNodeOrNull<CheckButton>("Panel/Margin/Rows/Scroll/Options/Subtitles");
        _highContrast = GetNodeOrNull<CheckButton>("Panel/Margin/Rows/Scroll/Options/HighContrast");
        _reducedMotion = GetNodeOrNull<CheckButton>("Panel/Margin/Rows/Scroll/Options/ReducedMotion");
        _invertY = GetNodeOrNull<CheckButton>("Panel/Margin/Rows/Scroll/Options/InvertY");
        _textScale = GetNodeOrNull<HSlider>("Panel/Margin/Rows/Scroll/Options/TextScale");
        _hudScale = GetNodeOrNull<HSlider>("Panel/Margin/Rows/Scroll/Options/HudScale");
        _safeArea = GetNodeOrNull<HSlider>("Panel/Margin/Rows/Scroll/Options/SafeArea");
        _masterVolume = GetNodeOrNull<HSlider>("Panel/Margin/Rows/Scroll/Options/MasterVolume");
        _status = GetNodeOrNull<Label>("Panel/Margin/Rows/Status");

        Populate();
        ConnectControls();

        Button? defaults = GetNodeOrNull<Button>("Panel/Margin/Rows/Buttons/Defaults");
        if (defaults != null) defaults.Pressed += RestoreDefaults;
        Button? controls = GetNodeOrNull<Button>("Panel/Margin/Rows/Buttons/Controls");
        if (controls != null) controls.Pressed += () => EmitSignal(SignalName.ScreenRequested, (int)UIScreenId.Controls);
    }

    private void ConnectControls()
    {
        if (_subtitles != null) _subtitles.Toggled += _ => Persist();
        if (_highContrast != null) _highContrast.Toggled += _ => Persist();
        if (_reducedMotion != null) _reducedMotion.Toggled += _ => Persist();
        if (_invertY != null) _invertY.Toggled += _ => Persist();
        if (_textScale != null) _textScale.DragEnded += _ => Persist();
        if (_hudScale != null) _hudScale.DragEnded += _ => Persist();
        if (_safeArea != null) _safeArea.DragEnded += _ => Persist();
        if (_masterVolume != null) _masterVolume.DragEnded += _ => Persist();
    }

    private void Populate()
    {
        if (_settings == null) return;
        UserInterfaceSettings current = _settings.Current;
        if (_subtitles != null) _subtitles.ButtonPressed = current.SubtitlesEnabled;
        if (_highContrast != null) _highContrast.ButtonPressed = current.HighContrast;
        if (_reducedMotion != null) _reducedMotion.ButtonPressed = current.ReducedMotion;
        if (_invertY != null) _invertY.ButtonPressed = current.InvertLookY;
        if (_textScale != null) _textScale.Value = current.TextScale;
        if (_hudScale != null) _hudScale.Value = current.HudScale;
        if (_safeArea != null) _safeArea.Value = current.SafeArea;
        if (_masterVolume != null) _masterVolume.Value = current.MasterVolume;
    }

    private void Persist()
    {
        if (_settings == null) return;
        _settings.Update(_settings.Current with
        {
            SubtitlesEnabled = _subtitles?.ButtonPressed ?? true,
            HighContrast = _highContrast?.ButtonPressed ?? false,
            ReducedMotion = _reducedMotion?.ButtonPressed ?? false,
            InvertLookY = _invertY?.ButtonPressed ?? false,
            TextScale = (float)(_textScale?.Value ?? 1.0),
            HudScale = (float)(_hudScale?.Value ?? 1.0),
            SafeArea = (float)(_safeArea?.Value ?? 1.0),
            MasterVolume = (float)(_masterVolume?.Value ?? 1.0),
        });
        if (_status != null) _status.Text = Tr("ui.settings.saved");
    }

    private void RestoreDefaults()
    {
        _settings?.RestoreDefaults();
        InputRebindStore.RestoreDefaults();
        Populate();
        if (_status != null) _status.Text = Tr("ui.settings.defaults_restored");
    }
}
