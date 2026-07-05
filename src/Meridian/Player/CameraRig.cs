using Godot;
using System;
using Meridian.Data;

namespace Meridian.Player;

/// <summary>
/// SpringArm3D-based camera rig handling smooth transitions between explore and aim states.
/// Enforces Section 5.4 requirements.
/// </summary>
public partial class CameraRig : SpringArm3D
{
    [Export] public CameraModeResource? ExploreMode { get; set; }
    [Export] public CameraModeResource? AimMode { get; set; }

    private Camera3D? _camera;
    private Node3D? _target;
    private float _yaw = 0f;
    private float _pitch = 0f;

    private float _currentSpringLength;
    private float _currentFov;
    private float _currentShoulderOffset;

    public override void _Ready()
    {
        _camera = GetNodeOrNull<Camera3D>("Camera3D");
        _target = GetParentOrNull<Node3D>();

        // Set defaults or load resources programmatically
        ExploreMode ??= new CameraModeResource();
        AimMode ??= new CameraModeResource
        {
            SpringLength = 1.5f,
            Fov = 60.0f,
            ShoulderOffset = 0.7f
        };

        _currentSpringLength = ExploreMode.SpringLength;
        _currentFov = ExploreMode.Fov;
        _currentShoulderOffset = ExploreMode.ShoulderOffset;

        // Hide cursor
        Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouseMotion && Godot.Input.MouseMode == Godot.Input.MouseModeEnum.Captured)
        {
            // Adjust camera look rotation based on mouse sensitivity
            _yaw -= mouseMotion.Relative.X * 0.003f;
            _pitch = Mathf.Clamp(_pitch - mouseMotion.Relative.Y * 0.003f, -Mathf.Pi / 3.0f, Mathf.Pi / 4.0f);
        }
    }

    public void UpdateCamera(bool isAiming, double delta)
    {
        if (_target == null || _camera == null) return;

        // Rotate camera rig
        Rotation = new Vector3(_pitch, _yaw, 0);

        // Interpolate camera modes parameters (Explore ↔ Aim)
        var targetMode = isAiming ? AimMode : ExploreMode;
        if (targetMode == null) return;

        float weight = (float)(targetMode.SmoothSpeed * delta);
        _currentSpringLength = Mathf.Lerp(_currentSpringLength, targetMode.SpringLength, weight);
        _currentFov = Mathf.Lerp(_currentFov, targetMode.Fov, weight);
        _currentShoulderOffset = Mathf.Lerp(_currentShoulderOffset, targetMode.ShoulderOffset, weight);

        SpringLength = _currentSpringLength;
        _camera.Fov = _currentFov;

        // Apply shoulder offset and pivot height offsets
        Position = targetMode.PivotOffset;
        _camera.Position = new Vector3(_currentShoulderOffset, 0, 0);

        // Update target rotation to follow yaw if moving, or during aim
        if (isAiming || Godot.Input.IsActionPressed("move_forward") || Godot.Input.IsActionPressed("move_backward") ||
            Godot.Input.IsActionPressed("move_left") || Godot.Input.IsActionPressed("move_right"))
        {
            _target.Rotation = new Vector3(_target.Rotation.X, _yaw, _target.Rotation.Z);
        }
    }
}
