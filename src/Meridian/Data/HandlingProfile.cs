using Godot;

namespace Meridian.Data;

/// <summary>
/// Data-driven definition Resource for a vehicle handling profile. Implements IVehicleHandlingProfile.
/// Enforces Section 8.0 requirements.
/// </summary>
[GlobalClass]
public partial class HandlingProfile : Resource, IVehicleHandlingProfile
{
    [Export] public string Id { get; set; } = "";
    [Export] public float MaxSpeed { get; set; } = 25.0f;
    [Export] public float Acceleration { get; set; } = 10.0f;
    [Export] public float SteeringLimit { get; set; } = 35.0f; // In degrees
    [Export] public float BrakingStrength { get; set; } = 20.0f;
    [Export] public float FuelBurnRate { get; set; } = 1.5f;
    [Export] public float Wheelbase { get; set; } = 2.6f; // Meters, axle-to-axle; steering feel per §11.1
    [Export] public float MaxLateralAcceleration { get; set; } = 9.0f; // m/s² (~0.9g road-car grip)
}
