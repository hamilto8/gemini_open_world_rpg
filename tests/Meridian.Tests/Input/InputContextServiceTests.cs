using System;
using Xunit;
using Meridian.Input;

namespace Meridian.Tests.Input;

public class InputContextServiceTests
{
    [Fact]
    public void DefaultContext_ShouldBeOnFoot()
    {
        var service = new InputContextService();
        Assert.Equal(InputContextType.OnFoot, service.CurrentContext);
    }

    [Fact]
    public void PushAndPop_ShouldMaintainContextStack()
    {
        var service = new InputContextService();
        service.PushContext(InputContextType.Vehicle);
        Assert.Equal(InputContextType.Vehicle, service.CurrentContext);

        service.PushContext(InputContextType.UI);
        Assert.Equal(InputContextType.UI, service.CurrentContext);

        service.PopContext();
        Assert.Equal(InputContextType.Vehicle, service.CurrentContext);

        service.PopContext();
        Assert.Equal(InputContextType.OnFoot, service.CurrentContext);
    }

    [Fact]
    public void PopOnDefault_ShouldNotThrowOrEmptyStack()
    {
        var service = new InputContextService();
        service.PopContext();
        Assert.Equal(InputContextType.OnFoot, service.CurrentContext);
    }

    [Fact]
    public void IsActionAllowed_ShouldRespectActiveContext()
    {
        var service = new InputContextService();
        
        // OnFoot allows jump and move
        Assert.True(service.IsActionAllowed("jump"));
        Assert.True(service.IsActionAllowed("move_forward"));
        Assert.False(service.IsActionAllowed("brake"));

        // Push Vehicle context: movement actions drive throttle/steer, brake is held, jump is blocked.
        service.PushContext(InputContextType.Vehicle);
        Assert.True(service.IsActionAllowed("move_forward")); // drives throttle (C3)
        Assert.True(service.IsActionAllowed("brake"));
        Assert.False(service.IsActionAllowed("jump"));
        Assert.True(service.IsActionAllowed("interact")); // allowed in both (exit)

        // Push UI context
        service.PushContext(InputContextType.UI);
        Assert.True(service.IsActionAllowed("ui_accept"));
        Assert.False(service.IsActionAllowed("move_forward"));
        Assert.False(service.IsActionAllowed("jump"));

        // Global actions always allowed
        Assert.True(service.IsActionAllowed("pause"));
        Assert.True(service.IsActionAllowed("console_toggle"));
    }
}
