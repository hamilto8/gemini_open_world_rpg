using System;
using System.IO;
using Godot;
using Meridian.Core;

namespace Meridian.UI;

/// <summary>
/// Scene-owned preferences service. It persists outside save slots, applies settings centrally, and
/// publishes changes so camera/audio/subtitle implementations can opt in without depending on UI nodes.
/// </summary>
public partial class UserInterfaceSettingsServiceNode : Node, IUserInterfaceSettingsService
{
    [Export(PropertyHint.File, "*.json")] public string SettingsPath { get; set; } = "user://preferences.json";

    private UserInterfaceSettingsStore? _store;
    public UserInterfaceSettings Current { get; private set; } = new();
    public event Action<UserInterfaceSettings>? Changed;

    public override void _EnterTree()
    {
        Services.Register<IUserInterfaceSettingsService>(this);
    }

    public override void _Ready()
    {
        _store = new UserInterfaceSettingsStore(ProjectSettings.GlobalizePath(SettingsPath));
        Apply(_store.Load(), persist: false);
    }

    public override void _ExitTree()
    {
        if (Services.TryGet<IUserInterfaceSettingsService>(out var current) && ReferenceEquals(current, this))
        {
            Services.Unregister<IUserInterfaceSettingsService>();
        }
    }

    public void Update(UserInterfaceSettings settings) => Apply(settings, persist: true);

    public void RestoreDefaults() => Apply(new UserInterfaceSettings(), persist: true);

    private void Apply(UserInterfaceSettings settings, bool persist)
    {
        Current = settings.Sanitize();
        TranslationServer.SetLocale(Current.Locale);
        SetBusVolume("Master", Current.MasterVolume);
        SetBusVolume("Music", Current.MusicVolume);
        SetBusVolume("SFX", Current.EffectsVolume);
        SetBusVolume("Dialogue", Current.DialogueVolume);

        if (persist)
        {
            try
            {
                _store?.Save(Current);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                GD.PushWarning($"[UI Settings] Could not persist preferences: {error.Message}");
            }
        }

        Changed?.Invoke(Current);
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new UserInterfaceSettingsChangedEvent(Current));
        }
    }

    private static void SetBusVolume(string busName, float linearVolume)
    {
        int busIndex = AudioServer.GetBusIndex(busName);
        if (busIndex >= 0)
        {
            AudioServer.SetBusVolumeDb(busIndex, Mathf.LinearToDb(Mathf.Max(linearVolume, 0.0001f)));
        }
    }
}
