using System;
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

    // --- Kinematic bicycle yaw (§11.1) -------------------------------------------------------
    // Sign convention: Godot forward is -Z and a positive RotateY turns left (CCW from above).
    // Steer is right-positive, so steering right while moving forward must produce a NEGATIVE yaw
    // rate. Signed Speed makes reversing flip the turn; zero speed produces zero yaw.

    [Fact]
    public void ZeroSpeed_FullSteer_ShouldNotYaw()
    {
        var motor = MakeMotor();
        // Steer hard with no throttle: parked and turning the wheel must not spin the body.
        motor.Step(new VehicleInput(Throttle: 0f, Steer: 1.0f, Brake: false), 0.1f);

        Assert.Equal(0f, motor.Speed, 3);
        Assert.Equal(0f, motor.YawRateRadians, 6);
    }

    [Fact]
    public void ForwardSteerRight_ShouldYawNegative_And_SteerLeft_Positive()
    {
        var right = MakeMotor(fuel: 50f);
        right.Step(new VehicleInput(Throttle: 1.0f, Steer: 1.0f, Brake: false), 0.5f);
        Assert.True(right.Speed > 0f);
        Assert.True(right.YawRateRadians < 0f, $"forward + right steer should yaw right (<0), got {right.YawRateRadians}");

        var left = MakeMotor(fuel: 50f);
        left.Step(new VehicleInput(Throttle: 1.0f, Steer: -1.0f, Brake: false), 0.5f);
        Assert.True(left.YawRateRadians > 0f, $"forward + left steer should yaw left (>0), got {left.YawRateRadians}");

        // Mirror-image steer at equal speed → equal magnitude, opposite sign.
        Assert.Equal(-right.YawRateRadians, left.YawRateRadians, 5);
    }

    [Fact]
    public void ReverseSteerRight_ShouldYawPositive_OppositeOfForward()
    {
        var motor = MakeMotor(fuel: 50f);
        // Reverse (throttle negative) drives Speed below zero; steering right must now yaw left (>0).
        motor.Step(new VehicleInput(Throttle: -1.0f, Steer: 1.0f, Brake: false), 0.5f);

        Assert.True(motor.Speed < 0f, $"expected reverse motion, got {motor.Speed}");
        Assert.True(motor.YawRateRadians > 0f, $"reverse + right steer should yaw left (>0), got {motor.YawRateRadians}");
    }

    [Fact]
    public void ZeroSteer_AtSpeed_ShouldNotYaw()
    {
        var motor = MakeMotor(fuel: 50f);
        motor.Step(new VehicleInput(Throttle: 1.0f, Steer: 0f, Brake: false), 1.0f);

        Assert.True(motor.Speed > 0f);
        Assert.Equal(0f, motor.YawRateRadians, 6);
    }

    [Fact]
    public void HeadingIntegration_ForwardSteerRight_SweepsNinetyDegreesInDerivedTime()
    {
        // Fixed, known geometry so the expected turn time is analytic, not hand-waved.
        var profile = new BasicVehicleHandlingProfile
        {
            Acceleration = 10.0f,
            MaxSpeed = 30.0f,
            SteeringLimit = 40.0f,
            BrakingStrength = 15.0f,
            FuelBurnRate = 0f, // isolate steering geometry from fuel exhaustion
            Wheelbase = 2.6f,
            MaxLateralAcceleration = float.MaxValue, // disable the grip cap: pure bicycle geometry
        };
        var motor = new VehicleMotorModel(profile, fuel: 100f, maxFuel: 100f);
        const float dt = 1f / 60f;

        // Prime to top speed with no steer so the turn happens at a constant, known speed.
        for (int i = 0; i < 240; i++)
        {
            motor.Step(new VehicleInput(1f, 0f, false), dt);
        }
        Assert.Equal(30f, motor.Speed, 2);

        // Bicycle model at full right lock: |yaw| = (v/L)*tan(steer); t90 = (pi/2)/|yaw|.
        float steerRad = 40f * MathF.PI / 180f;
        float expectedYaw = -(30f / 2.6f) * MathF.Tan(steerRad); // negative → turning right
        float expectedTime = (MathF.PI / 2f) / MathF.Abs(expectedYaw);

        float heading = 0f;
        float elapsed = 0f;
        while (MathF.Abs(heading) < MathF.PI / 2f && elapsed < 5f)
        {
            motor.Step(new VehicleInput(1f, 1f, false), dt);
            heading += motor.YawRateRadians * dt; // same integration the Node does via RotateY
            elapsed += dt;
        }

        Assert.True(heading < 0f, $"forward + right steer should end pointing right (heading < 0), got {heading}");
        // Discrete integration overshoots by at most one step; allow a small slack around the analytic time.
        Assert.InRange(elapsed, expectedTime - dt, expectedTime + 2f * dt);
    }

    [Fact]
    public void YawRate_AtSpeed_IsCappedByMaxLateralAcceleration()
    {
        var profile = new BasicVehicleHandlingProfile
        {
            Acceleration = 50.0f,
            MaxSpeed = 30.0f,
            SteeringLimit = 40.0f,
            BrakingStrength = 15.0f,
            FuelBurnRate = 0f,
            Wheelbase = 2.6f,
            MaxLateralAcceleration = 9.0f,
        };
        var motor = new VehicleMotorModel(profile, fuel: 100f, maxFuel: 100f);
        const float dt = 1f / 60f;

        for (int i = 0; i < 120; i++)
        {
            motor.Step(new VehicleInput(1f, Steer: 1f, Brake: false), dt);
        }
        Assert.Equal(30f, motor.Speed, 2);

        // Lateral acceleration is |v*w|; at full lock and top speed raw bicycle yaw would be
        // ~(30/2.6)*tan(40 deg) = 9.7 rad/s (a 3 m radius at 30 m/s). The grip cap must bind instead.
        Assert.Equal(9.0f / 30.0f, MathF.Abs(motor.YawRateRadians), 4);
    }
}
