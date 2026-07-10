namespace Meridian.Data;

/// <summary>
/// Interface representing handling profile traits required by the Vehicle simulation.
/// Allows unit tests to mock vehicle behavior without instantiating Godot Resource classes.
/// </summary>
public interface IVehicleHandlingProfile
{
    string Id { get; }
    float MaxSpeed { get; }
    float Acceleration { get; }
    float SteeringLimit { get; }
    float BrakingStrength { get; }
    float FuelBurnRate { get; } // Fuel units consumed per second of throttle
    float Wheelbase { get; } // Axle-to-axle distance in meters; sets bicycle-model turn rate (§11.1)
    float MaxLateralAcceleration { get; } // Grip proxy in m/s²; caps yaw rate at speed (§11.1)
}
