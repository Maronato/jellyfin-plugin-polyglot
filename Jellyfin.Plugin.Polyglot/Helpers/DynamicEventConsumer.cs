using System;
using System.Reflection;
using MediaBrowser.Controller.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyglot.Helpers;

/// <summary>
/// Provides dynamic event consumer registration to avoid compile-time type dependencies
/// on event args that reference moved types (e.g., User entity moved in Jellyfin 10.11).
/// </summary>
public static class DynamicEventConsumer
{
    /// <summary>
    /// Attempts to register a dynamic event consumer for the given event args type name.
    /// This avoids compile-time type dependencies on event args that reference types
    /// that may have moved between Jellyfin versions.
    /// </summary>
    /// <param name="services">The service collection to register to.</param>
    /// <param name="eventArgsTypeName">Full type name of the event args (e.g., "Jellyfin.Data.Events.Users.UserCreatedEventArgs").</param>
    /// <param name="handlerType">The type that contains the handler method.</param>
    /// <param name="handlerMethodName">Name of the async method that handles the event (takes object, returns Task).</param>
    /// <param name="logger">Optional logger for registration warnings.</param>
    /// <returns>True if registration succeeded, false if the event args type couldn't be found.</returns>
    public static bool TryRegister(
        IServiceCollection services,
        string eventArgsTypeName,
        Type handlerType,
        string handlerMethodName,
        ILogger? logger = null)
    {
        // Try to find the event args type at runtime
        var eventArgsType = FindType(eventArgsTypeName);
        if (eventArgsType == null)
        {
            logger?.LogWarning(
                "DynamicEventConsumer: Could not find event args type '{TypeName}'. " +
                "Event consumer will not be registered. This may indicate an incompatible Jellyfin version.",
                eventArgsTypeName);
            return false;
        }

        // Get the IEventConsumer<T> interface type
        var eventConsumerOpenType = FindType("MediaBrowser.Controller.Events.IEventConsumer`1");
        if (eventConsumerOpenType == null)
        {
            logger?.LogWarning(
                "DynamicEventConsumer: Could not find IEventConsumer<T> interface. " +
                "Event consumer will not be registered.");
            return false;
        }

        // Create closed generic: IEventConsumer<UserCreatedEventArgs>
        var eventConsumerClosedType = eventConsumerOpenType.MakeGenericType(eventArgsType);

        // Create the dynamic consumer type: DynamicEventConsumerImpl<UserCreatedEventArgs>
        var implType = typeof(DynamicEventConsumerImpl<>).MakeGenericType(eventArgsType);

        // Register with a factory that creates the impl with the handler
        services.AddSingleton(eventConsumerClosedType, sp =>
        {
            var handler = sp.GetRequiredService(handlerType);
            var method = handlerType.GetMethod(handlerMethodName, BindingFlags.Public | BindingFlags.Instance);

            if (method == null)
            {
                throw new InvalidOperationException(
                    $"Handler method '{handlerMethodName}' not found on type '{handlerType.Name}'");
            }

            // Create instance of DynamicEventConsumerImpl<T>
            var instance = Activator.CreateInstance(implType, handler, method);
            return instance!;
        });

        logger?.LogDebug(
            "DynamicEventConsumer: Successfully registered event consumer for '{TypeName}'",
            eventArgsTypeName);

        return true;
    }

    /// <summary>
    /// Finds a type by name across all loaded assemblies.
    /// </summary>
    private static Type? FindType(string fullTypeName)
    {
        // Try direct lookup first
        var type = Type.GetType(fullTypeName);
        if (type != null)
        {
            return type;
        }

        // Search all loaded assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                type = assembly.GetType(fullTypeName);
                if (type != null)
                {
                    return type;
                }
            }
            catch
            {
                // Skip assemblies that can't be searched
            }
        }

        return null;
    }
}

/// <summary>
/// Generic implementation of IEventConsumer that delegates to a handler via reflection.
/// The type parameter T is closed at runtime to avoid compile-time dependencies.
/// </summary>
/// <typeparam name="T">The event args type.</typeparam>
/// <remarks>
/// This class is instantiated via Activator.CreateInstance at runtime.
/// Note: We can safely implement IEventConsumer&lt;T&gt; because it's an open generic type.
/// The actual event args type is only resolved at runtime via MakeGenericType(),
/// avoiding compile-time dependencies on types that reference moved User entity.
/// </remarks>
#pragma warning disable CA1812 // Class is instantiated via reflection
internal sealed class DynamicEventConsumerImpl<T> : IEventConsumer<T>
    where T : EventArgs
#pragma warning restore CA1812
{
    private readonly object _handler;
    private readonly MethodInfo _method;

    public DynamicEventConsumerImpl(object handler, MethodInfo method)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _method = method ?? throw new ArgumentNullException(nameof(method));
    }

    /// <inheritdoc />
    public async System.Threading.Tasks.Task OnEvent(T eventArgs)
    {
        try
        {
            var result = _method.Invoke(_handler, new object[] { eventArgs! });
            if (result is System.Threading.Tasks.Task task)
            {
                await task.ConfigureAwait(false);
            }
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            // Unwrap reflection exceptions for better error messages
            throw ex.InnerException;
        }
    }
}
