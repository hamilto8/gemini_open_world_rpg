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

    [Fact]
    public void Publish_ShouldInvokeAllHandlers_ForSameEvent()
    {
        var bus = new EventBus();
        int a = 0, b = 0;
        using var s1 = bus.Subscribe<TestEvent>(_ => a++);
        using var s2 = bus.Subscribe<TestEvent>(_ => b++);

        bus.Publish(new TestEvent("x", 1));

        Assert.Equal(1, a);
        Assert.Equal(1, b);
    }

    [Fact]
    public void Unsubscribe_DuringPublish_ShouldNotAffectInFlightDispatch()
    {
        var bus = new EventBus();
        int first = 0, second = 0;
        IDisposable? sub2 = null;

        var sub1 = bus.Subscribe<TestEvent>(_ =>
        {
            first++;
            sub2?.Dispose(); // remove another handler mid-dispatch
        });
        sub2 = bus.Subscribe<TestEvent>(_ => second++);

        // The immutable snapshot means the already-scheduled second handler still runs this time.
        bus.Publish(new TestEvent("x", 1));
        Assert.Equal(1, first);
        Assert.Equal(1, second);

        // Next publish reflects the unsubscribe.
        bus.Publish(new TestEvent("y", 2));
        Assert.Equal(2, first);
        Assert.Equal(1, second);

        sub1.Dispose();
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var bus = new EventBus();
        int count = 0;
        var sub = bus.Subscribe<TestEvent>(_ => count++);

        sub.Dispose();
        sub.Dispose(); // second dispose must be a harmless no-op

        bus.Publish(new TestEvent("x", 1));
        Assert.Equal(0, count);
    }
}
