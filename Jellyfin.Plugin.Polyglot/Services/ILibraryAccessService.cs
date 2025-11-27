using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Polyglot.Models;

namespace Jellyfin.Plugin.Polyglot.Services;

/// <summary>
/// Service for managing user library access based on language assignments.
/// </summary>
public interface ILibraryAccessService
{
    /// <summary>
    /// Updates a user's library access based on their language assignment.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task UpdateUserLibraryAccessAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles a user's library access with their language assignment.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if changes were made.</returns>
    Task<bool> ReconcileUserAccessAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles all users' library access with their language assignments.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of users with changes.</returns>
    Task<int> ReconcileAllUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the library IDs that a user should have access to based on their language.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>Collection of library IDs.</returns>
    IEnumerable<Guid> GetExpectedLibraryAccess(Guid userId);

    /// <summary>
    /// Enables plugin management for all users, setting them to default language.
    /// This will set EnableAllFolders=false and configure their library access.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of users enabled.</returns>
    Task<int> EnableAllUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables plugin management for a user, optionally restoring EnableAllFolders.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="restoreFullAccess">If true, sets EnableAllFolders back to true.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task DisableUserAsync(Guid userId, bool restoreFullAccess = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds specific libraries to a user's access.
    /// Used when mirrors are deleted and sources need to be preserved.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="libraryIds">The library IDs to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task AddLibrariesToUserAccessAsync(Guid userId, IEnumerable<Guid> libraryIds, CancellationToken cancellationToken = default);
}

