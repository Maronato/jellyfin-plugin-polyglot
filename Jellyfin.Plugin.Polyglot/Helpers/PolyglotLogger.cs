using System;
using Jellyfin.Plugin.Polyglot.Services;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyglot.Helpers;

/// <summary>
/// Extension methods for ILogger that also log to the Polyglot debug buffer.
/// </summary>
public static class PolyglotLoggerExtensions
{
    /// <summary>
    /// Logs a debug message and captures it in the debug buffer.
    /// </summary>
    public static void PolyglotDebug(this ILogger logger, string message, params object?[] args)
    {
        logger.LogDebug(message, args);
        LogToBuffer("Debug", message, args);
    }

    /// <summary>
    /// Logs an information message and captures it in the debug buffer.
    /// </summary>
    public static void PolyglotInfo(this ILogger logger, string message, params object?[] args)
    {
        logger.LogInformation(message, args);
        LogToBuffer("Information", message, args);
    }

    /// <summary>
    /// Logs a warning message and captures it in the debug buffer.
    /// </summary>
    public static void PolyglotWarning(this ILogger logger, string message, params object?[] args)
    {
        logger.LogWarning(message, args);
        LogToBuffer("Warning", message, args);
    }

    /// <summary>
    /// Logs a warning message with exception and captures it in the debug buffer.
    /// </summary>
    public static void PolyglotWarning(this ILogger logger, Exception? exception, string message, params object?[] args)
    {
        logger.LogWarning(exception, message, args);
        LogToBuffer("Warning", message, args, exception);
    }

    /// <summary>
    /// Logs an error message and captures it in the debug buffer.
    /// </summary>
    public static void PolyglotError(this ILogger logger, string message, params object?[] args)
    {
        logger.LogError(message, args);
        LogToBuffer("Error", message, args);
    }

    /// <summary>
    /// Logs an error message with exception and captures it in the debug buffer.
    /// </summary>
    public static void PolyglotError(this ILogger logger, Exception? exception, string message, params object?[] args)
    {
        logger.LogError(exception, message, args);
        LogToBuffer("Error", message, args, exception);
    }

    private static void LogToBuffer(string level, string message, object?[] args, Exception? exception = null)
    {
        try
        {
            var formattedMessage = args.Length > 0 ? FormatMessage(message, args) : message;
            DebugReportService.LogToBufferStatic(level, formattedMessage, exception?.Message);
        }
        catch
        {
            // Don't let logging failures affect the application
        }
    }

    private static string FormatMessage(string message, object?[] args)
    {
        try
        {
            // Handle structured logging format {0}, {1}, etc.
            return string.Format(message, args);
        }
        catch
        {
            // If format fails (e.g., named placeholders like {userId}), just return the template
            return message;
        }
    }
}

