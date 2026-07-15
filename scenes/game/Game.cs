using Godot;
using Meridian.Core;
using Meridian.Core.Validation;
using Meridian.Input;

namespace Meridian.Scenes;

/// <summary>
/// Scene-glue script for the main Game root scene.
/// Connects the HUD, console, and 3D environment.
/// </summary>
public partial class Game : Node
{
    [Export] public bool ValidateContentOnBoot { get; set; } = true;

    public override void _Ready()
    {
        GD.Print("[GameScene] Main game scene loaded.");

        // Set default input context to OnFoot
        var inputService = Services.Get<IInputContextService>();
        inputService.Reset();

        // This scene is active gameplay, so advance the app state machine to Playing (V5).
        // Deferred so it runs after the GameDirector's own boot -> MainMenu deferred transition,
        // yielding Boot -> MainMenu -> Playing per doc §3.4.
        if (Services.TryGet<IGameDirector>(out var director) && director != null)
        {
            Callable.From(() => director.TransitionTo(GameState.Playing)).CallDeferred();
        }

        if (ValidateContentOnBoot)
        {
            ValidateContent();
        }

        // Print initial instructions
        GD.Print("[GameScene] Press the Backquote/Tilde (`) key to open the Debug Console.");
    }

    private void ValidateContent()
    {
        string projectRoot = ProjectSettings.GlobalizePath("res://");
        var validator = new ContentValidator(projectRoot);
        if (validator.ValidateContent(out var errors))
        {
            GD.Print("[ContentValidator] Boot validation passed.");
            return;
        }

        GD.PushError($"[ContentValidator] Boot validation failed with {errors.Count} issue(s).");
        foreach (var error in errors)
        {
            GD.PushError($"[ContentValidator] {error}");
        }

        if (DisplayServer.GetName().Equals("headless", System.StringComparison.OrdinalIgnoreCase))
        {
            GetTree().Quit(2);
        }
    }
}
