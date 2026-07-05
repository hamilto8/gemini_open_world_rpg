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

/// <summary>
/// Basic mock implementation of IVehicleHandlingProfile for unit testing.
/// </summary>
public class BasicVehicleHandlingProfile : IVehicleHandlingProfile
{
    public string Id { get; set; } = "default_car";
    public float MaxSpeed { get; set; } = 20.0f;
    public float Acceleration { get; set; } = 8.0f;
    public float SteeringLimit { get; set; } = 40.0f;
    public float BrakingStrength { get; set; } = 15.0f;
    public float FuelBurnRate { get; set; } = 2.0f;
}
