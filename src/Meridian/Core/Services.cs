using System;
using System.Collections.Generic;

namespace Meridian.Core;

/// <summary>
/// Static service locator exposing interfaces (IWorldClock, IEventBus, ISaveService, etc.).
/// Populated at boot by the autoloads registering themselves.
/// Consumers depend on interfaces, allowing headless unit tests to install fakes or mock services.
/// </summary>
public static class Services
{
    private static readonly Dictionary<Type, object> _services = new();

    /// <summary>
    /// Registers a service instance under its interface type T.
    /// </summary>
    public static void Register<T>(T service) where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        _services[typeof(T)] = service;
    }

    /// <summary>
    /// Retrieves a registered service of type T. Throws if not registered.
    /// </summary>
    public static T Get<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var service))
        {
            return (T)service;
        }
        throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered in Services locator.");
    }

    /// <summary>
    /// Attempts to retrieve a registered service of type T.
    /// </summary>
    public static bool TryGet<T>(out T? service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var obj))
        {
            service = (T)obj;
            return true;
        }
        service = null;
        return false;
    }

    /// <summary>
    /// Removes the service registered under interface type T, if any.
    /// Nodes should call this in <c>_ExitTree</c> to keep the locator symmetric and
    /// avoid handing out stale references after scene teardown.
    /// </summary>
    public static void Unregister<T>() where T : class
    {
        _services.Remove(typeof(T));
    }

    /// <summary>
    /// Clears all registered services. Useful for unit testing teardown and application reset.
    /// </summary>
    public static void Reset()
    {
        _services.Clear();
    }
}
