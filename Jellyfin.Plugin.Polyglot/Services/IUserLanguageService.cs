using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Polyglot.Models;

namespace Jellyfin.Plugin.Polyglot.Services;

/// <summary>
/// Service for managing user language assignments.
/// </summary>
public interface IUserLanguageService
{
    /// <summary>
    /// Assigns a language alternative to a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="alternativeId">The language alternative ID (null to clear assignment).</param>
    /// <param name="setBy">The source of the assignment ("admin", "user-sync", "auto").</param>
    /// <param name="manuallySet">Whether this is a manual override that blocks automatic updates.</param>
    /// <param name="isPluginManaged">Whether the plugin should manage this user's library access.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task AssignLanguageAsync(Guid userId, Guid? alternativeId, string setBy, bool manuallySet = false, bool isPluginManaged = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all users with their language assignments.
    /// </summary>
    /// <returns>Collection of user information with language assignments.</returns>
    IEnumerable<UserInfo> GetAllUsersWithLanguages();

    /// <summary>
    /// Removes user assignment data when a user is deleted.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    void RemoveUser(Guid userId);
}

