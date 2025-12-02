using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Polyglot.Helpers;

/// <summary>
/// A wrapper around IUserManager that provides version-agnostic access to user operations.
/// This avoids compile-time dependencies on methods that return User type, which moved
/// from Jellyfin.Data.Entities.User (10.10.x) to Jellyfin.Database.Implementations.Entities.User (10.11.x).
/// </summary>
public sealed class PolyglotUserManager
{
    private readonly IUserManager _userManager;
    private readonly Type _userManagerType;

    // Cached reflection info
    private static MethodInfo? _getUserByIdMethod;
    private static PropertyInfo? _usersProperty;
    private static MethodInfo? _updateUserAsyncMethod;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolyglotUserManager"/> class.
    /// </summary>
    /// <param name="userManager">The underlying Jellyfin IUserManager.</param>
    public PolyglotUserManager(IUserManager userManager)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _userManagerType = userManager.GetType();
    }

    /// <summary>
    /// Gets the underlying IUserManager for passing to other Jellyfin APIs.
    /// </summary>
    public IUserManager UnderlyingManager => _userManager;

    /// <summary>
    /// Gets a user by their ID, wrapped in PolyglotUser.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <returns>A PolyglotUser instance, or null if not found.</returns>
    public PolyglotUser? GetUserById(Guid userId)
    {
        var method = GetCachedMethod(ref _getUserByIdMethod, "GetUserById", typeof(Guid));
        if (method == null)
        {
            return null;
        }

        try
        {
            var result = method.Invoke(_userManager, new object[] { userId });
            return result == null ? null : new PolyglotUser(result);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets all users, wrapped in PolyglotUser.
    /// </summary>
    /// <returns>An enumerable of PolyglotUser instances.</returns>
    public IEnumerable<PolyglotUser> GetUsers()
    {
        var property = GetCachedProperty(ref _usersProperty, "Users");
        if (property == null)
        {
            return Enumerable.Empty<PolyglotUser>();
        }

        try
        {
            var result = property.GetValue(_userManager);
            if (result is System.Collections.IEnumerable enumerable)
            {
                return enumerable.Cast<object>().Select(u => new PolyglotUser(u));
            }
        }
        catch
        {
            // Fall through
        }

        return Enumerable.Empty<PolyglotUser>();
    }

    /// <summary>
    /// Updates a user asynchronously.
    /// </summary>
    /// <param name="user">The PolyglotUser to update.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task UpdateUserAsync(PolyglotUser user)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        var method = GetCachedMethod(ref _updateUserAsyncMethod, "UpdateUserAsync");
        if (method == null)
        {
            throw new InvalidOperationException("UpdateUserAsync method not found on IUserManager");
        }

        try
        {
            var result = method.Invoke(_userManager, new[] { user.UnderlyingUser });
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
            }
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private MethodInfo? GetCachedMethod(ref MethodInfo? cache, string methodName, params Type[] paramTypes)
    {
        if (cache != null)
        {
            return cache;
        }

        cache = paramTypes.Length > 0
            ? _userManagerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, paramTypes, null)
            : _userManagerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);

        return cache;
    }

    private PropertyInfo? GetCachedProperty(ref PropertyInfo? cache, string propertyName)
    {
        if (cache != null)
        {
            return cache;
        }

        cache = _userManagerType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        return cache;
    }
}

/// <summary>
/// Extension methods for creating PolyglotUserManager instances.
/// </summary>
public static class PolyglotUserManagerExtensions
{
    /// <summary>
    /// Wraps an IUserManager in a PolyglotUserManager for version-agnostic access.
    /// </summary>
    /// <param name="userManager">The user manager to wrap.</param>
    /// <returns>A PolyglotUserManager instance.</returns>
    public static PolyglotUserManager ToPolyglot(this IUserManager userManager)
    {
        return new PolyglotUserManager(userManager);
    }
}

