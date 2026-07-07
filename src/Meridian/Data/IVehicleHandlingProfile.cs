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
}
