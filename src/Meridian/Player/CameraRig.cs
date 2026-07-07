using Godot;
using Meridian.Core;
using Meridian.Data;

namespace Meridian.Player;

/// <summary>
/// SpringArm3D-based camera rig handling smooth transitions between explore and aim states.
/// Look input arrives via the <see cref="InputFrame"/> (already context-gated by the controller),
/// so the rig never polls raw input or owns the mouse mode. Enforces Section 5.4 requirements.
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
    }

    public void UpdateCamera(InputFrame input, bool isAiming, double delta)
    {
        if (_target == null || _camera == null) return;

        // Interpolate camera modes parameters (Explore ↔ Aim)
        var targetMode = (isAiming ? AimMode : ExploreMode) ?? ExploreMode;
        if (targetMode == null) return;

        // Apply look from the (context-gated) input frame, scaled by the mode's data-driven sensitivity.
        float sensitivity = targetMode.MouseSensitivity;
        _yaw -= input.LookX * sensitivity;
        _pitch = Mathf.Clamp(_pitch - input.LookY * sensitivity, -Mathf.Pi / 3.0f, Mathf.Pi / 4.0f);

        Rotation = new Vector3(_pitch, _yaw, 0);

        float weight = (float)(targetMode.SmoothSpeed * delta);
        _currentSpringLength = Mathf.Lerp(_currentSpringLength, targetMode.SpringLength, weight);
        _currentFov = Mathf.Lerp(_currentFov, targetMode.Fov, weight);
        _currentShoulderOffset = Mathf.Lerp(_currentShoulderOffset, targetMode.ShoulderOffset, weight);

        SpringLength = _currentSpringLength;
        _camera.Fov = _currentFov;

        // Apply shoulder offset and pivot height offsets
        Position = targetMode.PivotOffset;
        _camera.Position = new Vector3(_currentShoulderOffset, 0, 0);

        // Turn the body to face the camera yaw when aiming or moving (movement read from the frame).
        bool moving = Mathf.Abs(input.MoveX) > 0.01f || Mathf.Abs(input.MoveY) > 0.01f;
        if (isAiming || moving)
        {
            _target.Rotation = new Vector3(_target.Rotation.X, _yaw, _target.Rotation.Z);
        }
    }
}
