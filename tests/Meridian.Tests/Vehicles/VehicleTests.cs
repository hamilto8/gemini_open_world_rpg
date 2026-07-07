using Xunit;
using Meridian.Data;
using Meridian.Vehicles;

namespace Meridian.Tests.Vehicles;

/// <summary>
/// Exercises the real <see cref="VehicleMotorModel"/> (not inline math), pinning the
/// forward-is-positive throttle convention that vehicles share with on-foot movement (C4).
/// </summary>
public class VehicleTests
{
    private static VehicleMotorModel MakeMotor(float fuel = 50f, IVehicleHandlingProfile? profile = null)
    {
        profile ??= new BasicVehicleHandlingProfile
        {
            Acceleration = 10.0f,
            MaxSpeed = 30.0f,
            FuelBurnRate = 5.0f,
            BrakingStrength = 15.0f,
            SteeringLimit = 40.0f,
        };
        return new VehicleMotorModel(profile, fuel, 100f);
    }

    [Fact]
    public void PositiveThrottle_ShouldBurnFuelAndAccelerate()
    {
        var motor = MakeMotor(fuel: 50f);

        // Forward is positive throttle (matches InputFrame.MoveY += 1 for move_forward).
        motor.Step(new VehicleInput(Throttle: 1.0f, Steer: 0f, Brake: false), delta: 0.5f);

        Assert.Equal(47.5f, motor.Fuel, 3);
        Assert.Equal(5.0f, motor.Speed, 3);
    }

    [Fact]
    public void NegativeThrottle_ShouldNotDriveForward()
    {
        var motor = MakeMotor(fuel: 50f);

        // A sign inversion (the original C4 bug) would accelerate the vehicle forward here.
        motor.Step(new VehicleInput(Throttle: -1.0f, Steer: 0f, Brake: false), delta: 0.5f);

        Assert.True(motor.Speed <= 0f, $"expected reverse/no forward motion, got {motor.Speed}");
    }

    [Fact]
    public void EmptyTank_ShouldPreventAcceleration()
    {
        var motor = MakeMotor(fuel: 0f);

        motor.Step(new VehicleInput(Throttle: 1.0f, Steer: 0f, Brake: false), delta: 1.0f);

        Assert.Equal(0f, motor.Fuel, 3);
        Assert.Equal(0f, motor.Speed, 3);
    }

    [Fact]
    public void Brake_ShouldDecelerate_WhileHeld()
    {
        var motor = MakeMotor(fuel: 50f);
        motor.Step(new VehicleInput(1.0f, 0f, false), 1.0f); // build up speed
        float moving = motor.Speed;
        Assert.True(moving > 0f);

        motor.Step(new VehicleInput(0f, 0f, Brake: true), 1.0f);

        Assert.True(motor.Speed < moving, $"brake should reduce speed: {moving} -> {motor.Speed}");
    }

    [Fact]
    public void Steering_ShouldClampToProfileLimit()
    {
        var motor = MakeMotor();
        motor.Step(new VehicleInput(0f, Steer: 5.0f, Brake: false), 0.1f); // over-range input
        Assert.Equal(40.0f, motor.SteeringAngle, 3);
    }
}
