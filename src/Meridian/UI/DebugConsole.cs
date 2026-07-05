using Godot;
using System;
using Meridian.Core;
using Meridian.Core.Save;
using Meridian.Environment;
using Meridian.Input;

namespace Meridian.UI;

/// <summary>
/// Developer Debug Console supporting execution of cheat and testing commands.
/// Enforces Section 20.2 requirements.
/// </summary>
public partial class DebugConsole : Control
{
    private LineEdit? _inputField;
    private RichTextLabel? _outputLog;
    private bool _isVisible = false;

    public override void _Ready()
    {
        _inputField = GetNodeOrNull<LineEdit>("InputField");
        _outputLog = GetNodeOrNull<RichTextLabel>("OutputLog");

        if (_inputField == null)
        {
            // Programmatic fallback layout
            var panel = new Panel { Size = new Vector2(600, 300) };
            AddChild(panel);

            var vbox = new VBoxContainer { Size = new Vector2(580, 280), Position = new Vector2(10, 10) };
            panel.AddChild(vbox);

            _outputLog = new RichTextLabel
            {
                Name = "OutputLog",
                CustomMinimumSize = new Vector2(0, 220),
                ScrollFollowing = true,
                Text = "Project Meridian Debug Console. Type 'help' for commands.\n"
            };
            vbox.AddChild(_outputLog);

            _inputField = new LineEdit
            {
                Name = "InputField",
                PlaceholderText = "Enter command..."
            };
            vbox.AddChild(_inputField);
        }

        _inputField.TextSubmitted += OnCommandSubmitted;

        // Hide by default
        Visible = false;
        _isVisible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("console_toggle"))
        {
            ToggleConsole();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ToggleConsole()
    {
        _isVisible = !_isVisible;
        Visible = _isVisible;

        var inputService = Services.Get<IInputContextService>();

        if (_isVisible)
        {
            _inputField?.GrabFocus();
            inputService.PushContext(InputContextType.Console);
        }
        else
        {
            _inputField?.ReleaseFocus();
            inputService.PopContext();
        }
    }

    private void LogMessage(string message, Color? color = null)
    {
        if (_outputLog == null) return;

        string colorHex = color?.ToHtml(false) ?? "ffffff";
        _outputLog.AppendText($"[color=#{colorHex}]{message}[/color]\n");
    }

    private void OnCommandSubmitted(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        _inputField?.Clear();
        LogMessage($"> {text}", new Color(0.7f, 0.7f, 0.7f));

        string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        string command = parts[0].ToLower();

        try
        {
            switch (command)
            {
                case "help":
                    LogMessage("Available Commands:");
                    LogMessage("  set-time <hour> [minute] - Sets the game clock time");
                    LogMessage("  set-weather <weatherId> [intensity] - Sets current weather");
                    LogMessage("  save <slotName> - Atomically saves the game state to slot");
                    LogMessage("  load <slotName> - Loads game state from slot");
                    LogMessage("  help - Shows this list");
                    break;

                case "set-time":
                    if (parts.Length < 2)
                    {
                        LogMessage("Usage: set-time <hour> [minute]", new Color(0.9f, 0.3f, 0.3f));
                        break;
                    }
                    int hour = int.Parse(parts[1]);
                    int minute = parts.Length > 2 ? int.Parse(parts[2]) : 0;
                    var clock = Services.Get<IWorldClock>();
                    clock.SetTime(hour, minute);
                    LogMessage($"Time set to {hour:D2}:{minute:D2}", new Color(0.3f, 0.9f, 0.3f));
                    break;

                case "set-weather":
                    if (parts.Length < 2)
                    {
                        LogMessage("Usage: set-weather <weatherId> [intensity]", new Color(0.9f, 0.3f, 0.3f));
                        break;
                    }
                    string weatherId = parts[1];
                    float intensity = parts.Length > 2 ? float.Parse(parts[2]) : 1.0f;
                    var weather = Services.Get<IWeatherSystem>();
                    weather.ForceWeather(weatherId, intensity);
                    LogMessage($"Weather forced to '{weatherId}' with intensity {intensity}", new Color(0.3f, 0.9f, 0.3f));
                    break;

                case "save":
                    if (parts.Length < 2)
                    {
                        LogMessage("Usage: save <slotName>", new Color(0.9f, 0.3f, 0.3f));
                        break;
                    }
                    string saveSlot = parts[1];
                    var saveService = Services.Get<ISaveService>();
                    saveService.SaveGame(saveSlot, "Debug Console Save");
                    LogMessage($"Game saved successfully to '{saveSlot}'", new Color(0.3f, 0.9f, 0.3f));
                    break;

                case "load":
                    if (parts.Length < 2)
                    {
                        LogMessage("Usage: load <slotName>", new Color(0.9f, 0.3f, 0.3f));
                        break;
                    }
                    string loadSlot = parts[1];
                    var saveServiceLoad = Services.Get<ISaveService>();
                    if (saveServiceLoad.LoadGame(loadSlot))
                    {
                        LogMessage($"Game loaded successfully from '{loadSlot}'", new Color(0.3f, 0.9f, 0.3f));
                    }
                    else
                    {
                        LogMessage($"Failed to load game from '{loadSlot}' (file missing or corrupt)", new Color(0.9f, 0.3f, 0.3f));
                    }
                    break;

                default:
                    LogMessage($"Unknown command: '{command}'. Type 'help' for list of commands.", new Color(0.9f, 0.3f, 0.3f));
                    break;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error executing command: {ex.Message}", new Color(0.9f, 0.3f, 0.3f));
        }
    }
}
