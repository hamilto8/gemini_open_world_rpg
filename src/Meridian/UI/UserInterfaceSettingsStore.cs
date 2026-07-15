using System;
using System.IO;
using System.Text.Json;

namespace Meridian.UI;

/// <summary>Small, engine-independent JSON repository with durable replace semantics.</summary>
public sealed class UserInterfaceSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public UserInterfaceSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public UserInterfaceSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new UserInterfaceSettings();
            }

            string json = File.ReadAllText(_path);
            return (JsonSerializer.Deserialize<UserInterfaceSettings>(json, JsonOptions)
                ?? new UserInterfaceSettings()).Sanitize();
        }
        catch (IOException)
        {
            return new UserInterfaceSettings();
        }
        catch (JsonException)
        {
            return new UserInterfaceSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new UserInterfaceSettings();
        }
    }

    public void Save(UserInterfaceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings.Sanitize(), JsonOptions));
        File.Move(temporaryPath, _path, overwrite: true);
    }
}
