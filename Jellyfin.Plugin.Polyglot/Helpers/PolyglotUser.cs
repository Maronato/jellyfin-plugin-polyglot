using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Jellyfin.Plugin.Polyglot.Helpers;

/// <summary>
/// A wrapper around Jellyfin's User entity that provides version-agnostic access.
/// This handles the namespace change from Jellyfin.Data.Entities.User (10.10.x)
/// to Jellyfin.Database.Implementations.Entities.User (10.11.x).
/// Also handles enum type migrations (PermissionKind, PreferenceKind moved namespaces).
/// </summary>
public sealed class PolyglotUser
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> IdPropertyCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> UsernamePropertyCache = new();
    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new();

    private readonly object _user;
    private readonly Type _userType;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolyglotUser"/> class.
    /// </summary>
    /// <param name="user">The underlying Jellyfin User object (from either 10.10.x or 10.11.x).</param>
    /// <exception cref="ArgumentNullException">Thrown when user is null.</exception>
    public PolyglotUser(object user)
    {
        _user = user ?? throw new ArgumentNullException(nameof(user));
        _userType = user.GetType();
    }

    /// <summary>
    /// Gets the underlying User object for passing back to Jellyfin APIs.
    /// </summary>
    public object UnderlyingUser => _user;

    /// <summary>
    /// Gets the user's unique identifier.
    /// </summary>
    public Guid Id => GetProperty<Guid>("Id");

    /// <summary>
    /// Gets the user's username.
    /// </summary>
    public string Username => GetProperty<string>("Username") ?? string.Empty;

    /// <summary>
    /// Checks whether the user has the specified permission.
    /// </summary>
    /// <param name="permissionKind">The permission kind value (use <see cref="PolyglotPermissionKind"/> constants).</param>
    /// <returns>True if the user has the permission, false otherwise.</returns>
    public bool HasPermission(int permissionKind)
    {
        var enumValue = ConvertToRuntimeEnum(permissionKind, "PermissionKind");
        if (enumValue == null)
        {
            return false;
        }

        return InvokeMethod<bool>("HasPermission", enumValue);
    }

    /// <summary>
    /// Sets the specified permission for the user.
    /// </summary>
    /// <param name="permissionKind">The permission kind value (use <see cref="PolyglotPermissionKind"/> constants).</param>
    /// <param name="value">The value to set.</param>
    public void SetPermission(int permissionKind, bool value)
    {
        var enumValue = ConvertToRuntimeEnum(permissionKind, "PermissionKind");
        if (enumValue == null)
        {
            return;
        }

        InvokeMethod("SetPermission", enumValue, value);
    }

    /// <summary>
    /// Gets the user's preference values for the specified preference kind.
    /// </summary>
    /// <param name="preferenceKind">The preference kind value (use <see cref="PolyglotPreferenceKind"/> constants).</param>
    /// <returns>An array of preference values, or empty array if not found.</returns>
    public string[] GetPreference(int preferenceKind)
    {
        var enumValue = ConvertToRuntimeEnum(preferenceKind, "PreferenceKind");
        if (enumValue == null)
        {
            return Array.Empty<string>();
        }

        return InvokeMethod<string[]>("GetPreference", enumValue) ?? Array.Empty<string>();
    }

    /// <summary>
    /// Sets the user's preference values for the specified preference kind.
    /// </summary>
    /// <param name="preferenceKind">The preference kind value (use <see cref="PolyglotPreferenceKind"/> constants).</param>
    /// <param name="values">The values to set.</param>
    public void SetPreference(int preferenceKind, string[] values)
    {
        var enumValue = ConvertToRuntimeEnum(preferenceKind, "PreferenceKind");
        if (enumValue == null)
        {
            return;
        }

        InvokeMethod("SetPreference", enumValue, values);
    }

    /// <summary>
    /// Creates a PolyglotUser from a nullable object.
    /// </summary>
    /// <param name="user">The user object, or null.</param>
    /// <returns>A PolyglotUser instance, or null if the input was null.</returns>
    public static PolyglotUser? FromObject(object? user)
    {
        return user == null ? null : new PolyglotUser(user);
    }

    /// <summary>
    /// Converts an integer value to the runtime enum type.
    /// Handles the enum type migration between 10.10.x and 10.11.x.
    /// </summary>
    private static object? ConvertToRuntimeEnum(int value, string enumName)
    {
        // Try different locations where the enum might be
        var typeNames = new[]
        {
            // 10.10.x location
            $"Jellyfin.Data.Enums.{enumName}, Jellyfin.Data",
            // 10.11.x location
            $"Jellyfin.Database.Implementations.Enums.{enumName}, Jellyfin.Database.Implementations"
        };

        foreach (var typeName in typeNames)
        {
            var enumType = TypeCache.GetOrAdd(typeName, name =>
            {
                try
                {
                    return Type.GetType(name);
                }
                catch
                {
                    return null;
                }
            });

            if (enumType != null && enumType.IsEnum)
            {
                return Enum.ToObject(enumType, value);
            }
        }

        return null;
    }

    private T? GetProperty<T>(string propertyName)
    {
        var cache = propertyName switch
        {
            "Id" => IdPropertyCache,
            "Username" => UsernamePropertyCache,
            _ => null
        };

        PropertyInfo? prop;
        if (cache != null)
        {
            prop = cache.GetOrAdd(_userType, t => t.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance));
        }
        else
        {
            prop = _userType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        }

        if (prop == null)
        {
            return default;
        }

        var value = prop.GetValue(_user);
        return value is T typedValue ? typedValue : default;
    }

    private T? InvokeMethod<T>(string methodName, params object[] args)
    {
        var result = InvokeMethodInternal(methodName, args);
        return result is T typedResult ? typedResult : default;
    }

    private void InvokeMethod(string methodName, params object[] args)
    {
        InvokeMethodInternal(methodName, args);
    }

    private object? InvokeMethodInternal(string methodName, object[] args)
    {
        // First, try to find an instance method on the User type
        var method = FindInstanceMethod(methodName, args);
        if (method != null)
        {
            return method.Invoke(_user, args);
        }

        // If not found, try to find an extension method
        // In 10.11.x, methods like HasPermission moved to UserEntityExtensions
        method = FindExtensionMethod(methodName, args);
        if (method != null)
        {
            // Extension methods are static; first param is 'this' (the user object)
            var extensionArgs = new object[args.Length + 1];
            extensionArgs[0] = _user;
            Array.Copy(args, 0, extensionArgs, 1, args.Length);
            return method.Invoke(null, extensionArgs);
        }

        throw new MissingMethodException($"Method '{methodName}' not found on User type or extension classes");
    }

    private MethodInfo? FindInstanceMethod(string methodName, object[] args)
    {
        var argTypes = args.Select(a => a.GetType()).ToArray();
        return _userType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance,
            null,
            argTypes,
            null);
    }

    private MethodInfo? FindExtensionMethod(string methodName, object[] args)
    {
        // Extension method types to search
        // In 10.11.x, User methods moved to Jellyfin.Data.UserEntityExtensions
        var extensionTypeNames = new[]
        {
            "Jellyfin.Data.UserEntityExtensions, Jellyfin.Data"
        };

        foreach (var typeName in extensionTypeNames)
        {
            var extensionType = TypeCache.GetOrAdd(typeName, name =>
            {
                try
                {
                    return Type.GetType(name);
                }
                catch
                {
                    return null;
                }
            });

            if (extensionType == null)
            {
                continue;
            }

            // Extension methods have 'this' as first parameter
            var argTypes = new Type[args.Length + 1];
            argTypes[0] = _userType;
            for (int i = 0; i < args.Length; i++)
            {
                argTypes[i + 1] = args[i].GetType();
            }

            // Look for method with matching name
            var methods = extensionType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == methodName);

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length != argTypes.Length)
                {
                    continue;
                }

                // Check if first parameter is compatible with User type (or IHasPermissions interface)
                var firstParam = parameters[0].ParameterType;
                if (!firstParam.IsAssignableFrom(_userType))
                {
                    continue;
                }

                // Check remaining parameters
                bool match = true;
                for (int i = 1; i < parameters.Length; i++)
                {
                    if (!parameters[i].ParameterType.IsAssignableFrom(argTypes[i]))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return method;
                }
            }
        }

        return null;
    }
}

/// <summary>
/// Permission kind constants matching Jellyfin's PermissionKind enum values.
/// Use these instead of the Jellyfin enum directly for cross-version compatibility.
/// </summary>
public static class PolyglotPermissionKind
{
    /// <summary>Whether the user is an administrator.</summary>
    public const int IsAdministrator = 0;

    /// <summary>Whether the user is hidden.</summary>
    public const int IsHidden = 1;

    /// <summary>Whether the user is disabled.</summary>
    public const int IsDisabled = 2;

    /// <summary>Whether the user has access to all folders.</summary>
    public const int EnableAllFolders = 16;
}

/// <summary>
/// Preference kind constants matching Jellyfin's PreferenceKind enum values.
/// Use these instead of the Jellyfin enum directly for cross-version compatibility.
/// </summary>
public static class PolyglotPreferenceKind
{
    /// <summary>A list of enabled folders.</summary>
    public const int EnabledFolders = 5;
}

/// <summary>
/// Extension methods for creating PolyglotUser instances.
/// </summary>
public static class PolyglotUserExtensions
{
    /// <summary>
    /// Converts a User object to a PolyglotUser wrapper.
    /// </summary>
    /// <param name="user">The user object.</param>
    /// <returns>A PolyglotUser instance, or null if input was null.</returns>
    public static PolyglotUser? ToPolyglotUser(this object? user)
    {
        return PolyglotUser.FromObject(user);
    }
}
