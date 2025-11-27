using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Polyglot.Models;
using MediaBrowser.Common;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyglot.Services;

/// <summary>
/// Service for generating debug reports for troubleshooting.
/// </summary>
public partial class DebugReportService : IDebugReportService
{
    private readonly IApplicationHost _applicationHost;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<DebugReportService> _logger;

    // Static circular buffer for recent logs (accessible across the plugin)
    private static readonly ConcurrentQueue<LogEntryInfo> LogBuffer = new();
    private const int MaxLogEntries = 500;
    private static readonly TimeSpan MaxLogAge = TimeSpan.FromHours(1);

    /// <summary>
    /// Static method to log to the buffer without requiring a service instance.
    /// Used by extension methods and other components.
    /// </summary>
    /// <param name="level">Log level.</param>
    /// <param name="message">Log message.</param>
    /// <param name="exception">Optional exception message.</param>
    public static void LogToBufferStatic(string level, string message, string? exception = null)
    {
        var entry = new LogEntryInfo
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = SanitizeLogMessage(message),
            Exception = exception != null ? SanitizeLogMessage(exception) : null
        };

        LogBuffer.Enqueue(entry);

        // Trim old entries
        while (LogBuffer.Count > MaxLogEntries)
        {
            LogBuffer.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DebugReportService"/> class.
    /// </summary>
    public DebugReportService(
        IApplicationHost applicationHost,
        ILibraryManager libraryManager,
        ILogger<DebugReportService> logger)
    {
        _applicationHost = applicationHost;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public void LogToBuffer(string level, string message, string? exception = null)
    {
        LogToBufferStatic(level, message, exception);
    }

    /// <inheritdoc />
    public async Task<DebugReport> GenerateReportAsync(CancellationToken cancellationToken = default)
    {
        var report = new DebugReport
        {
            GeneratedAt = DateTime.UtcNow,
            Environment = GetEnvironmentInfo(),
            Configuration = GetConfigurationSummary(),
            MirrorHealth = await GetMirrorHealthAsync(cancellationToken).ConfigureAwait(false),
            UserDistribution = GetUserDistribution(),
            Libraries = GetLibrarySummaries(),
            OtherPlugins = GetOtherPlugins(),
            RecentLogs = GetRecentLogs()
        };

        return report;
    }

    /// <inheritdoc />
    public async Task<string> GenerateMarkdownReportAsync(CancellationToken cancellationToken = default)
    {
        var report = await GenerateReportAsync(cancellationToken).ConfigureAwait(false);
        return FormatAsMarkdown(report);
    }

    private EnvironmentInfo GetEnvironmentInfo()
    {
        var pluginVersion = Plugin.Instance?.Version?.ToString() ?? "Unknown";
        var jellyfinVersion = _applicationHost.ApplicationVersionString;

        return new EnvironmentInfo
        {
            PluginVersion = pluginVersion,
            JellyfinVersion = jellyfinVersion,
            OperatingSystem = RuntimeInformation.OSDescription,
            DotNetVersion = Environment.Version.ToString(),
            Architecture = RuntimeInformation.ProcessArchitecture.ToString()
        };
    }

    private ConfigurationSummary GetConfigurationSummary()
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null)
        {
            return new ConfigurationSummary();
        }

        var totalMirrors = config.LanguageAlternatives.Sum(a => a.MirroredLibraries.Count);
        var managedUsers = config.UserLanguages.Count(u => u.IsPluginManaged);

        return new ConfigurationSummary
        {
            LanguageAlternativeCount = config.LanguageAlternatives.Count,
            TotalMirrorCount = totalMirrors,
            ManagedUserCount = managedUsers,
            AutoManageNewUsers = config.AutoManageNewUsers,
            SyncAfterLibraryScan = config.SyncMirrorsAfterLibraryScan,
            LdapIntegrationEnabled = config.EnableLdapIntegration,
            ExcludedExtensionCount = config.ExcludedExtensions.Count,
            ExcludedDirectoryCount = config.ExcludedDirectories.Count
        };
    }

    private async Task<List<MirrorHealthInfo>> GetMirrorHealthAsync(CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null)
        {
            return new List<MirrorHealthInfo>();
        }

        var results = new List<MirrorHealthInfo>();
        var existingLibraryIds = GetExistingLibraryIds();
        var altIndex = 0;

        foreach (var alternative in config.LanguageAlternatives)
        {
            altIndex++;
            var mirrorIndex = 0;

            foreach (var mirror in alternative.MirroredLibraries)
            {
                mirrorIndex++;
                cancellationToken.ThrowIfCancellationRequested();

                var sourceExists = existingLibraryIds.Contains(mirror.SourceLibraryId);
                var targetExists = mirror.TargetLibraryId.HasValue && existingLibraryIds.Contains(mirror.TargetLibraryId.Value);
                var targetPathExists = !string.IsNullOrEmpty(mirror.TargetPath) && Directory.Exists(mirror.TargetPath);

                var lastSync = mirror.LastSyncedAt.HasValue
                    ? FormatTimeAgo(mirror.LastSyncedAt.Value)
                    : "Never";

                results.Add(new MirrorHealthInfo
                {
                    AlternativeName = $"Alt_{altIndex}",
                    SourceLibrary = $"Library_{mirrorIndex}",
                    Status = mirror.Status.ToString(),
                    LastSync = lastSync,
                    FileCount = mirror.LastSyncFileCount,
                    SourceExists = sourceExists,
                    TargetExists = targetExists,
                    TargetPathExists = targetPathExists,
                    LastError = SanitizeErrorMessage(mirror.LastError)
                });
            }
        }

        return results;
    }

    private List<UserDistributionInfo> GetUserDistribution()
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null)
        {
            return new List<UserDistributionInfo>();
        }

        var distribution = new List<UserDistributionInfo>();

        // Count users per alternative
        var managedUsers = config.UserLanguages.Where(u => u.IsPluginManaged).ToList();
        
        // Count users with no specific alternative (default)
        var defaultCount = managedUsers.Count(u => u.SelectedAlternativeId == null);
        if (defaultCount > 0)
        {
            distribution.Add(new UserDistributionInfo
            {
                Language = "Default (source libraries)",
                UserCount = defaultCount
            });
        }

        // Group by non-null alternatives
        var usersByAlt = managedUsers
            .Where(u => u.SelectedAlternativeId.HasValue)
            .GroupBy(u => u.SelectedAlternativeId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        // Per alternative
        var altIndex = 0;
        foreach (var alt in config.LanguageAlternatives)
        {
            altIndex++;
            var count = usersByAlt.GetValueOrDefault(alt.Id, 0);
            distribution.Add(new UserDistributionInfo
            {
                Language = $"Alt_{altIndex} ({alt.LanguageCode})",
                UserCount = count
            });
        }

        // Not managed
        var notManagedCount = config.UserLanguages.Count(u => !u.IsPluginManaged);
        if (notManagedCount > 0)
        {
            distribution.Add(new UserDistributionInfo
            {
                Language = "Not managed by plugin",
                UserCount = notManagedCount
            });
        }

        return distribution;
    }

    private List<LibrarySummaryInfo> GetLibrarySummaries()
    {
        var config = Plugin.Instance?.Configuration;
        var virtualFolders = _libraryManager.GetVirtualFolders();

        // Build set of mirror library IDs
        var mirrorIds = new HashSet<Guid>();
        if (config != null)
        {
            foreach (var alt in config.LanguageAlternatives)
            {
                foreach (var mirror in alt.MirroredLibraries)
                {
                    if (mirror.TargetLibraryId.HasValue)
                    {
                        mirrorIds.Add(mirror.TargetLibraryId.Value);
                    }
                }
            }
        }

        var results = new List<LibrarySummaryInfo>();
        var libIndex = 0;

        foreach (var folder in virtualFolders)
        {
            libIndex++;
            var folderId = Guid.TryParse(folder.ItemId, out var id) ? id : Guid.Empty;
            var isMirror = mirrorIds.Contains(folderId);
            var metadataLang = folder.LibraryOptions?.PreferredMetadataLanguage ?? "default";

            results.Add(new LibrarySummaryInfo
            {
                Name = $"Library_{libIndex}",
                Type = folder.CollectionType?.ToString() ?? "mixed",
                IsMirror = isMirror,
                MetadataLanguage = metadataLang
            });
        }

        return results;
    }

    private List<PluginSummaryInfo> GetOtherPlugins()
    {
        var plugins = _applicationHost.GetExports<IPlugin>();
        var polyglotId = Plugin.Instance?.Id ?? Guid.Empty;

        return plugins
            .Where(p => p.Id != polyglotId)
            .Select(p => new PluginSummaryInfo
            {
                Name = p.Name,
                Version = p.Version?.ToString() ?? "Unknown"
            })
            .OrderBy(p => p.Name)
            .ToList();
    }

    private static List<LogEntryInfo> GetRecentLogs()
    {
        var cutoff = DateTime.UtcNow - MaxLogAge;

        return LogBuffer
            .Where(e => e.Timestamp >= cutoff)
            .OrderByDescending(e => e.Timestamp)
            .Take(100) // Limit output
            .ToList();
    }

    private HashSet<Guid> GetExistingLibraryIds()
    {
        return _libraryManager.GetVirtualFolders()
            .Select(f => Guid.TryParse(f.ItemId, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
    }

    private static string FormatTimeAgo(DateTime time)
    {
        var span = DateTime.UtcNow - time;

        if (span.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (span.TotalHours < 1)
        {
            return $"{(int)span.TotalMinutes}m ago";
        }

        if (span.TotalDays < 1)
        {
            return $"{(int)span.TotalHours}h ago";
        }

        return $"{(int)span.TotalDays}d ago";
    }

    private static string? SanitizeErrorMessage(string? error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return null;
        }

        // Remove file paths
        error = PathPattern().Replace(error, "[path]");

        // Truncate long messages
        if (error.Length > 200)
        {
            error = error.Substring(0, 197) + "...";
        }

        return error;
    }

    private static string SanitizeLogMessage(string message)
    {
        // Remove file paths
        message = PathPattern().Replace(message, "[path]");

        // Remove potential usernames in common patterns
        message = UsernamePattern().Replace(message, "$1[user]$2");

        // Remove GUIDs that might be user IDs (but keep for context)
        // We'll leave GUIDs as they're not PII on their own

        return message;
    }

    [GeneratedRegex(@"[A-Za-z]:\\[^\s""'<>|]+|/(?:home|Users|media|mnt|data|var)[^\s""'<>|]+", RegexOptions.IgnoreCase)]
    private static partial Regex PathPattern();

    [GeneratedRegex(@"(user[_\s]*[:=]?\s*)[^\s,;]+(\s|,|;|$)", RegexOptions.IgnoreCase)]
    private static partial Regex UsernamePattern();

    private static string FormatAsMarkdown(DebugReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Polyglot Debug Report");
        sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-ddTHH:mm:ssZ}");
        sb.AppendLine();

        // Environment
        sb.AppendLine("## Environment");
        sb.AppendLine($"- **Plugin Version:** {report.Environment.PluginVersion}");
        sb.AppendLine($"- **Jellyfin Version:** {report.Environment.JellyfinVersion}");
        sb.AppendLine($"- **OS:** {report.Environment.OperatingSystem}");
        sb.AppendLine($"- **.NET:** {report.Environment.DotNetVersion}");
        sb.AppendLine($"- **Architecture:** {report.Environment.Architecture}");
        sb.AppendLine();

        // Configuration
        sb.AppendLine("## Configuration Summary");
        sb.AppendLine($"- Language Alternatives: {report.Configuration.LanguageAlternativeCount}");
        sb.AppendLine($"- Total Mirrors: {report.Configuration.TotalMirrorCount}");
        sb.AppendLine($"- Managed Users: {report.Configuration.ManagedUserCount}");
        sb.AppendLine($"- Auto-manage new users: {(report.Configuration.AutoManageNewUsers ? "Yes" : "No")}");
        sb.AppendLine($"- Sync after library scan: {(report.Configuration.SyncAfterLibraryScan ? "Yes" : "No")}");
        sb.AppendLine($"- LDAP Integration: {(report.Configuration.LdapIntegrationEnabled ? "Enabled" : "Disabled")}");
        sb.AppendLine($"- Excluded extensions: {report.Configuration.ExcludedExtensionCount}");
        sb.AppendLine($"- Excluded directories: {report.Configuration.ExcludedDirectoryCount}");
        sb.AppendLine();

        // Mirror Health
        if (report.MirrorHealth.Count > 0)
        {
            sb.AppendLine("## Mirror Health");
            sb.AppendLine("| Alternative | Source | Status | Last Sync | Files | Source? | Target? | Path? | Error |");
            sb.AppendLine("|-------------|--------|--------|-----------|-------|---------|---------|-------|-------|");

            foreach (var mirror in report.MirrorHealth)
            {
                var statusIcon = mirror.Status switch
                {
                    "Synced" => "✓",
                    "Error" => "✗",
                    "Syncing" => "↻",
                    _ => "○"
                };

                sb.AppendLine($"| {mirror.AlternativeName} | {mirror.SourceLibrary} | {statusIcon} {mirror.Status} | {mirror.LastSync} | {mirror.FileCount?.ToString() ?? "-"} | {(mirror.SourceExists ? "✓" : "✗")} | {(mirror.TargetExists ? "✓" : "✗")} | {(mirror.TargetPathExists ? "✓" : "✗")} | {mirror.LastError ?? "-"} |");
            }

            sb.AppendLine();
        }

        // User Distribution
        if (report.UserDistribution.Count > 0)
        {
            sb.AppendLine("## User Distribution");
            foreach (var dist in report.UserDistribution)
            {
                sb.AppendLine($"- {dist.Language}: {dist.UserCount} users");
            }

            sb.AppendLine();
        }

        // Libraries
        if (report.Libraries.Count > 0)
        {
            sb.AppendLine("## Libraries");
            sb.AppendLine("| Name | Type | Is Mirror | Metadata Lang |");
            sb.AppendLine("|------|------|-----------|---------------|");

            foreach (var lib in report.Libraries)
            {
                sb.AppendLine($"| {lib.Name} | {lib.Type} | {(lib.IsMirror ? "Yes" : "No")} | {lib.MetadataLanguage} |");
            }

            sb.AppendLine();
        }

        // Other Plugins
        if (report.OtherPlugins.Count > 0)
        {
            sb.AppendLine("## Other Installed Plugins");
            foreach (var plugin in report.OtherPlugins)
            {
                sb.AppendLine($"- {plugin.Name}: {plugin.Version}");
            }

            sb.AppendLine();
        }

        // Recent Logs
        if (report.RecentLogs.Count > 0)
        {
            sb.AppendLine("<details>");
            sb.AppendLine("<summary>Recent Logs (click to expand)</summary>");
            sb.AppendLine();
            sb.AppendLine("```");

            foreach (var log in report.RecentLogs.OrderBy(l => l.Timestamp))
            {
                var levelShort = log.Level switch
                {
                    "Information" => "INF",
                    "Warning" => "WRN",
                    "Error" => "ERR",
                    "Debug" => "DBG",
                    "Critical" => "CRT",
                    _ => log.Level.Substring(0, Math.Min(3, log.Level.Length)).ToUpperInvariant()
                };

                sb.AppendLine($"[{log.Timestamp:HH:mm:ss} {levelShort}] {log.Message}");

                if (!string.IsNullOrEmpty(log.Exception))
                {
                    sb.AppendLine($"    Exception: {log.Exception}");
                }
            }

            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("</details>");
        }

        return sb.ToString();
    }
}

