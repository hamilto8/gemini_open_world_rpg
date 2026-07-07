using Godot;
using Meridian.Core;
using Meridian.Input;

namespace Meridian.Scenes;

/// <summary>
/// Scene-glue script for the main Game root scene.
/// Connects the HUD, console, and 3D environment.
/// </summary>
public partial class Game : Node
{
    public override void _Ready()
    {
        GD.Print("[GameScene] Main game scene loaded.");

        // Set default input context to OnFoot
        var inputService = Services.Get<IInputContextService>();
        inputService.Reset();

        // Print initial instructions
        GD.Print("[GameScene] Press the Backquote/Tilde (`) key to open the Debug Console.");
    }
}
