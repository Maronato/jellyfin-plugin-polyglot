using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Polyglot.Services;

/// <summary>
/// Service for generating debug reports for troubleshooting.
/// </summary>
public interface IDebugReportService
{
    /// <summary>
    /// Generates a comprehensive debug report.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The debug report.</returns>
    Task<DebugReport> GenerateReportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates the debug report as formatted Markdown for GitHub issues.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Markdown-formatted report string.</returns>
    Task<string> GenerateMarkdownReportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a message to the circular buffer.
    /// </summary>
    /// <param name="level">Log level.</param>
    /// <param name="message">Log message.</param>
    /// <param name="exception">Optional exception.</param>
    void LogToBuffer(string level, string message, string? exception = null);
}

/// <summary>
/// Debug report data structure.
/// </summary>
public class DebugReport
{
    /// <summary>
    /// Gets or sets the timestamp when the report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the environment information.
    /// </summary>
    public EnvironmentInfo Environment { get; set; } = new();

    /// <summary>
    /// Gets or sets the configuration summary.
    /// </summary>
    public ConfigurationSummary Configuration { get; set; } = new();

    /// <summary>
    /// Gets or sets the mirror health information.
    /// </summary>
    public List<MirrorHealthInfo> MirrorHealth { get; set; } = new();

    /// <summary>
    /// Gets or sets the user distribution.
    /// </summary>
    public List<UserDistributionInfo> UserDistribution { get; set; } = new();

    /// <summary>
    /// Gets or sets the library information.
    /// </summary>
    public List<LibrarySummaryInfo> Libraries { get; set; } = new();

    /// <summary>
    /// Gets or sets the other installed plugins.
    /// </summary>
    public List<PluginSummaryInfo> OtherPlugins { get; set; } = new();

    /// <summary>
    /// Gets or sets the recent log entries.
    /// </summary>
    public List<LogEntryInfo> RecentLogs { get; set; } = new();
}

/// <summary>
/// Environment information.
/// </summary>
public class EnvironmentInfo
{
    /// <summary>
    /// Gets or sets the plugin version.
    /// </summary>
    public string PluginVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin version.
    /// </summary>
    public string JellyfinVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operating system.
    /// </summary>
    public string OperatingSystem { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the .NET runtime version.
    /// </summary>
    public string DotNetVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the processor architecture.
    /// </summary>
    public string Architecture { get; set; } = string.Empty;
}

/// <summary>
/// Configuration summary.
/// </summary>
public class ConfigurationSummary
{
    /// <summary>
    /// Gets or sets the number of language alternatives.
    /// </summary>
    public int LanguageAlternativeCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of mirrors.
    /// </summary>
    public int TotalMirrorCount { get; set; }

    /// <summary>
    /// Gets or sets the number of managed users.
    /// </summary>
    public int ManagedUserCount { get; set; }

    /// <summary>
    /// Gets or sets whether auto-manage new users is enabled.
    /// </summary>
    public bool AutoManageNewUsers { get; set; }

    /// <summary>
    /// Gets or sets whether sync after library scan is enabled.
    /// </summary>
    public bool SyncAfterLibraryScan { get; set; }

    /// <summary>
    /// Gets or sets whether LDAP integration is enabled.
    /// </summary>
    public bool LdapIntegrationEnabled { get; set; }

    /// <summary>
    /// Gets or sets the number of excluded extensions.
    /// </summary>
    public int ExcludedExtensionCount { get; set; }

    /// <summary>
    /// Gets or sets the number of excluded directories.
    /// </summary>
    public int ExcludedDirectoryCount { get; set; }
}

/// <summary>
/// Mirror health information.
/// </summary>
public class MirrorHealthInfo
{
    /// <summary>
    /// Gets or sets the alternative name (anonymized).
    /// </summary>
    public string AlternativeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source library name (anonymized).
    /// </summary>
    public string SourceLibrary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the mirror status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last sync time.
    /// </summary>
    public string LastSync { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file count from last sync.
    /// </summary>
    public int? FileCount { get; set; }

    /// <summary>
    /// Gets or sets whether the source library exists.
    /// </summary>
    public bool SourceExists { get; set; }

    /// <summary>
    /// Gets or sets whether the target library exists.
    /// </summary>
    public bool TargetExists { get; set; }

    /// <summary>
    /// Gets or sets whether the target path exists.
    /// </summary>
    public bool TargetPathExists { get; set; }

    /// <summary>
    /// Gets or sets the last error (if any).
    /// </summary>
    public string? LastError { get; set; }
}

/// <summary>
/// User distribution by language.
/// </summary>
public class UserDistributionInfo
{
    /// <summary>
    /// Gets or sets the language name.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user count.
    /// </summary>
    public int UserCount { get; set; }
}

/// <summary>
/// Library summary information.
/// </summary>
public class LibrarySummaryInfo
{
    /// <summary>
    /// Gets or sets the library name (anonymized).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this is a mirror library.
    /// </summary>
    public bool IsMirror { get; set; }

    /// <summary>
    /// Gets or sets the metadata language.
    /// </summary>
    public string MetadataLanguage { get; set; } = string.Empty;
}

/// <summary>
/// Plugin summary information.
/// </summary>
public class PluginSummaryInfo
{
    /// <summary>
    /// Gets or sets the plugin name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the plugin version.
    /// </summary>
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Log entry information.
/// </summary>
public class LogEntryInfo
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the log level.
    /// </summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exception message (if any).
    /// </summary>
    public string? Exception { get; set; }
}

