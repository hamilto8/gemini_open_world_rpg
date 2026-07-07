using System;
using Meridian.Data;

namespace Meridian.Vehicles;

/// <summary>
/// Per-frame driving inputs consumed by <see cref="VehicleMotorModel"/>.
/// <c>Throttle</c> follows the shared movement convention: forward is positive (matches
/// <c>InputFrame.MoveY</c> and <c>MovementMotor</c>), so pressing forward accelerates.
/// </summary>
public readonly record struct VehicleInput(float Throttle, float Steer, bool Brake);

/// <summary>
/// Engine-free longitudinal + steering model for a wheeled vehicle. Deterministic and
/// unit-testable — the <see cref="VehicleAvatar"/> Node owns physics (gravity, MoveAndSlide)
/// and simply drives this model each physics frame (Section 11, Definition-vs-Instance).
/// </summary>
public sealed class VehicleMotorModel
{
    private const float ThrottleDeadzone = 0.05f;
    private const float ReverseSpeedFactor = 0.4f;
    private const float CoastFrictionFactor = 0.1f;

    private readonly IVehicleHandlingProfile _profile;

    public VehicleMotorModel(IVehicleHandlingProfile profile, float fuel, float maxFuel)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        MaxFuel = maxFuel;
        Fuel = Math.Clamp(fuel, 0f, maxFuel);
    }

    /// <summary>Signed speed along the vehicle's forward axis (negative = reversing), m/s.</summary>
    public float Speed { get; private set; }

    /// <summary>Remaining fuel units.</summary>
    public float Fuel { get; private set; }

    /// <summary>Fuel capacity.</summary>
    public float MaxFuel { get; }

    /// <summary>Current steering angle in degrees (clamped to the profile's limit).</summary>
    public float SteeringAngle { get; private set; }

    /// <summary>Advances the model by <paramref name="delta"/> seconds under <paramref name="input"/>.</summary>
    public void Step(VehicleInput input, float delta)
    {
        if (delta <= 0f)
        {
            return;
        }

        float steer = Math.Clamp(input.Steer, -1f, 1f);
        SteeringAngle = steer * _profile.SteeringLimit;

        float throttle = Math.Clamp(input.Throttle, -1f, 1f);
        bool hasFuel = Fuel > 0f;

        if (throttle > ThrottleDeadzone && hasFuel)
        {
            BurnFuel(delta);
            Speed = Math.Min(_profile.MaxSpeed, Speed + _profile.Acceleration * throttle * delta);
        }
        else if (throttle < -ThrottleDeadzone && hasFuel)
        {
            // Reverse (capped well below forward top speed).
            BurnFuel(delta);
            Speed = Math.Max(-_profile.MaxSpeed * ReverseSpeedFactor, Speed + _profile.Acceleration * throttle * delta);
        }
        else
        {
            // Coasting: engine braking / rolling friction toward rest.
            Speed = MoveToward(Speed, 0f, _profile.BrakingStrength * CoastFrictionFactor * delta);
        }

        if (input.Brake)
        {
            Speed = MoveToward(Speed, 0f, _profile.BrakingStrength * delta);
        }
    }

    private void BurnFuel(float delta)
    {
        Fuel = Math.Max(0f, Fuel - _profile.FuelBurnRate * delta);
    }

    private static float MoveToward(float from, float to, float maxStep)
    {
        if (Math.Abs(to - from) <= maxStep)
        {
            return to;
        }
        return from + Math.Sign(to - from) * maxStep;
    }
}
