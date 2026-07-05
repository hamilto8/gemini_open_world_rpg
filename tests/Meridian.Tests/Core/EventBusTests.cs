using System;
using Xunit;
using Meridian.Core;

namespace Meridian.Tests.Core;

public class EventBusTests
{
    private record TestEvent(string Message, int Value);
    private record AnotherEvent(bool Flag);

    [Fact]
    public void Publish_ShouldInvokeSubscribedHandler()
    {
        var bus = new EventBus();
        string? receivedMessage = null;
        int receivedValue = 0;

        using var subscription = bus.Subscribe<TestEvent>(ev =>
        {
            receivedMessage = ev.Message;
            receivedValue = ev.Value;
        });

        bus.Publish(new TestEvent("Hello", 42));

        Assert.Equal("Hello", receivedMessage);
        Assert.Equal(42, receivedValue);
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeHandler()
    {
        var bus = new EventBus();
        int callCount = 0;

        var subscription = bus.Subscribe<TestEvent>(_ => callCount++);
        bus.Publish(new TestEvent("1", 1));
        Assert.Equal(1, callCount);

        subscription.Dispose();
        bus.Publish(new TestEvent("2", 2));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Clear_ShouldRemoveAllHandlers()
    {
        var bus = new EventBus();
        int callCount1 = 0;
        int callCount2 = 0;

        bus.Subscribe<TestEvent>(_ => callCount1++);
        bus.Subscribe<AnotherEvent>(_ => callCount2++);

        bus.Clear();
        bus.Publish(new TestEvent("1", 1));
        bus.Publish(new AnotherEvent(true));

        Assert.Equal(0, callCount1);
        Assert.Equal(0, callCount2);
    }
}
