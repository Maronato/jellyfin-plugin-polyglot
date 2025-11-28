using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.Polyglot.Helpers;
using Jellyfin.Plugin.Polyglot.Models;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Polyglot.Configuration;

/// <summary>
/// Plugin configuration for the Polyglot plugin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        LanguageAlternatives = new List<LanguageAlternative>();
        UserLanguages = new List<UserLanguageConfig>();
        LdapGroupMappings = new List<LdapGroupMapping>();
        ExcludedExtensions = FileClassifier.DefaultExcludedExtensions.ToList();
        ExcludedDirectories = FileClassifier.DefaultExcludedDirectories.ToList();
    }

    /// <summary>
    /// Gets or sets a value indicating whether LDAP integration is enabled.
    /// </summary>
    public bool EnableLdapIntegration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether new users should be automatically managed by the plugin.
    /// When enabled, newly created users will be added to plugin management with the default language.
    /// </summary>
    public bool AutoManageNewUsers { get; set; }

    /// <summary>
    /// Gets or sets the default language alternative ID for new users.
    /// Null means users get "Default libraries" (access to source libraries only).
    /// </summary>
    public Guid? DefaultLanguageAlternativeId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether mirrors should be synced automatically after library scans.
    /// When disabled, mirrors will only sync via the scheduled task or manual trigger.
    /// Default is true to maintain backward compatibility.
    /// </summary>
    public bool SyncMirrorsAfterLibraryScan { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of configured language alternatives.
    /// </summary>
    public List<LanguageAlternative> LanguageAlternatives { get; set; }

    /// <summary>
    /// Gets or sets the per-user language assignments.
    /// </summary>
    public List<UserLanguageConfig> UserLanguages { get; set; }

    /// <summary>
    /// Gets or sets the LDAP group to language mappings.
    /// </summary>
    public List<LdapGroupMapping> LdapGroupMappings { get; set; }

    /// <summary>
    /// Gets or sets the time for daily user reconciliation task (in 24-hour format, e.g., "03:00").
    /// </summary>
    public string UserReconciliationTime { get; set; } = "03:00";

    /// <summary>
    /// Gets or sets the file extensions to exclude from hardlinking (metadata and images).
    /// Extensions should include the leading dot (e.g., ".nfo", ".jpg").
    /// </summary>
    public List<string> ExcludedExtensions { get; set; }

    /// <summary>
    /// Gets or sets the directory names to exclude from mirroring.
    /// These are directory names (not full paths) that will be skipped during mirroring.
    /// </summary>
    public List<string> ExcludedDirectories { get; set; }

    /// <summary>
    /// Gets the default excluded file extensions (read-only, from FileClassifier).
    /// </summary>
    public List<string> DefaultExcludedExtensions => FileClassifier.DefaultExcludedExtensions.ToList();

    /// <summary>
    /// Gets the default excluded directory names (read-only, from FileClassifier).
    /// </summary>
    public List<string> DefaultExcludedDirectories => FileClassifier.DefaultExcludedDirectories.ToList();
}

