using System;
using Xunit;
using Meridian.Core;

namespace Meridian.Tests.Core;

public class ServicesTests : IDisposable
{
    private interface ITestService { string GetName(); }
    private class TestService : ITestService { public string GetName() => "Test"; }

    public ServicesTests()
    {
        Services.Reset();
    }

    public void Dispose()
    {
        Services.Reset();
    }

    [Fact]
    public void RegisterAndGet_ShouldReturnRegisteredInstance()
    {
        var instance = new TestService();
        Services.Register<ITestService>(instance);

        var retrieved = Services.Get<ITestService>();
        Assert.Same(instance, retrieved);
        Assert.Equal("Test", retrieved.GetName());
    }

    [Fact]
    public void TryGet_ShouldReturnTrueWhenRegistered()
    {
        var instance = new TestService();
        Services.Register<ITestService>(instance);

        bool success = Services.TryGet<ITestService>(out var retrieved);
        Assert.True(success);
        Assert.Same(instance, retrieved);
    }

    [Fact]
    public void Get_ShouldThrowWhenNotRegistered()
    {
        Assert.Throws<InvalidOperationException>(() => Services.Get<ITestService>());
    }

    [Fact]
    public void Reset_ShouldClearAllServices()
    {
        Services.Register<ITestService>(new TestService());
        Services.Reset();
        Assert.False(Services.TryGet<ITestService>(out _));
    }
}
