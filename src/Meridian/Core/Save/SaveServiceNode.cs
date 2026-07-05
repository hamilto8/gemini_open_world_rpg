using Godot;
using Meridian.Core;

namespace Meridian.Core.Save;

/// <summary>
/// Autoload Node wrapper for the SaveService. Registers ISaveService with the Services locator at boot.
/// </summary>
public partial class SaveServiceNode : Node, ISaveService
{
    private SaveService? _service;

    public override void _EnterTree()
    {
        // Resolve user directory path
        string userDir = ProjectSettings.GlobalizePath("user://saves");
        _service = new SaveService(userDir);

        Services.Register<ISaveService>(this);
    }

    public void RegisterParticipant(ISaveParticipant participant)
    {
        _service?.RegisterParticipant(participant);
    }

    public void UnregisterParticipant(ISaveParticipant participant)
    {
        _service?.UnregisterParticipant(participant);
    }

    public void SaveGame(string slotName, string locationName = "Unknown Location")
    {
        _service?.SaveGame(slotName, locationName);
    }

    public bool LoadGame(string slotName)
    {
        return _service?.LoadGame(slotName) ?? false;
    }

    public bool SaveExists(string slotName)
    {
        return _service?.SaveExists(slotName) ?? false;
    }

    public void DeleteSave(string slotName)
    {
        _service?.DeleteSave(slotName);
    }
}
