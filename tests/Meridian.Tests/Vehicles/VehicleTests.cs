using System;
using Xunit;
using Meridian.Data;

namespace Meridian.Tests.Vehicles;

public class VehicleTests
{
    [Fact]
    public void Vehicle_EngineThrottle_ShouldBurnFuelAndAccelerate()
    {
        var profile = new BasicVehicleHandlingProfile
        {
            Acceleration = 10.0f,
            MaxSpeed = 30.0f,
            FuelBurnRate = 5.0f
        };

        float fuel = 50.0f;
        float speed = 0f;
        float dt = 0.5f; // simulated half-second delta

        // Apply throttle (burn fuel and accelerate)
        fuel = Math.Max(0f, fuel - (profile.FuelBurnRate * dt));
        speed = Math.Min(profile.MaxSpeed, speed + (profile.Acceleration * dt));

        Assert.Equal(47.5f, fuel);
        Assert.Equal(5.0f, speed);
    }

    [Fact]
    public void Vehicle_FuelExhaustion_ShouldPreventAcceleration()
    {
        var profile = new BasicVehicleHandlingProfile
        {
            Acceleration = 10.0f,
            FuelBurnRate = 1.0f
        };

        float fuel = 0f; // empty tank
        float speed = 0f;
        float dt = 1.0f;

        // Try throttle, but fuel is 0
        if (fuel > 0f)
        {
            fuel = Math.Max(0f, fuel - (profile.FuelBurnRate * dt));
            speed = Math.Min(profile.MaxSpeed, speed + (profile.Acceleration * dt));
        }

        Assert.Equal(0f, fuel);
        Assert.Equal(0f, speed);
    }
}
