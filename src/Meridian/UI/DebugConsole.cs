using Godot;
using System;
using System.Collections.Generic;
using Meridian.Core;
using Meridian.Core.Logic;
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

    /// <summary>Opens the console (used by the pause menu's "Debug Console" option).</summary>
    public void Open()
    {
        if (!_isVisible)
        {
            ToggleConsole();
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
                    LogMessage("  flag <id> [true|false] - Gets or sets a world flag");
                    LogMessage("  action <verb> [args...] - Runs a validated GameAction (e.g. action give_item medkit 2)");
                    LogMessage("  validate-content - Scans data/ for broken references and duplicate ids");
                    LogMessage("  help - Shows this list");
                    break;

                case "flag":
                {
                    if (parts.Length < 2)
                    {
                        LogMessage("Usage: flag <id> [true|false]", new Color(0.9f, 0.3f, 0.3f));
                        break;
                    }

                    if (!Services.TryGet<IWorldFlags>(out var flags) || flags == null)
                    {
                        LogMessage("World flags service is not registered.", new Color(0.9f, 0.3f, 0.3f));
                        break;
                    }

                    string flagId = parts[1];
                    if (parts.Length == 2)
                    {
                        LogMessage($"flag '{flagId}' = {flags.GetFlag(flagId)}", new Color(0.3f, 0.9f, 0.3f));
                    }
                    else if (!bool.TryParse(parts[2], out bool flagValue))
                    {
                        LogMessage($"Could not parse '{parts[2]}' as true|false.", new Color(0.9f, 0.3f, 0.3f));
                    }
                    else
                    {
                        flags.SetFlag(flagId, flagValue);
                        LogMessage($"flag '{flagId}' set to {flagValue}", new Color(0.3f, 0.9f, 0.3f));
                    }
                    break;
                }

                case "action":
                {
                    var dispatcher = new ActionDispatcher();
                    if (parts.Length < 2)
                    {
                        LogMessage("Usage: action <verb> [args...]. Registered verbs:");
                        foreach (var usage in dispatcher.UsageLines)
                        {
                            LogMessage($"  {usage}");
                        }
                        break;
                    }

                    string verb = parts[1];
                    var actionArgs = new List<string>(parts.Length - 2);
                    for (int i = 2; i < parts.Length; i++)
                    {
                        actionArgs.Add(parts[i]);
                    }

                    // Real engine seams for the two scene-bound effects (§10.4): teleport_player moves
                    // the possessed body's global transform; spawn_scene instantiates under the live
                    // current scene. Kept local to this construction site so ServicesActionContext
                    // itself stays engine-free (§3.5).
                    var actionContext = new ServicesActionContext(
                        warn: msg => LogMessage($"  {msg}", new Color(0.9f, 0.7f, 0.3f)),
                        teleport: (x, y, z) =>
                        {
                            if (Services.TryGet<IPlayerController>(out var pc) && pc?.PossessedEntity is Node3D body)
                            {
                                body.GlobalPosition = new Vector3(x, y, z);
                            }
                            else
                            {
                                LogMessage("  TeleportPlayer dropped: nothing possessed.", new Color(0.9f, 0.7f, 0.3f));
                            }
                        },
                        spawnScene: (scenePath, x, y, z) =>
                        {
                            var packed = ResourceLoader.Load<PackedScene>(scenePath);
                            if (packed == null)
                            {
                                return false;
                            }

                            var instantiated = packed.Instantiate();
                            if (instantiated is not Node3D node)
                            {
                                instantiated?.QueueFree();
                                return false;
                            }

                            var currentScene = GetTree().CurrentScene;
                            if (currentScene == null)
                            {
                                node.QueueFree();
                                return false;
                            }

                            currentScene.AddChild(node);
                            node.GlobalPosition = new Vector3(x, y, z);
                            return true;
                        });

                    if (dispatcher.TryDispatch(verb, actionArgs, actionContext, out string dispatchError))
                    {
                        LogMessage($"Dispatched '{verb}'.", new Color(0.3f, 0.9f, 0.3f));
                    }
                    else
                    {
                        LogMessage($"Action failed: {dispatchError}", new Color(0.9f, 0.3f, 0.3f));
                    }
                    break;
                }

                case "validate-content":
                {
                    string projectRoot = ProjectSettings.GlobalizePath("res://");
                    var validator = new Meridian.Core.Validation.ContentValidator(projectRoot);
                    if (validator.ValidateContent(out var errors))
                    {
                        LogMessage("Content validation passed.", new Color(0.3f, 0.9f, 0.3f));
                    }
                    else
                    {
                        LogMessage($"Content validation found {errors.Count} issue(s):", new Color(0.9f, 0.3f, 0.3f));
                        foreach (var error in errors)
                        {
                            LogMessage($"  {error}", new Color(0.9f, 0.5f, 0.3f));
                        }
                    }
                    break;
                }

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
