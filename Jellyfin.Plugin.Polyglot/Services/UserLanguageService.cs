using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Polyglot.Helpers;
using Jellyfin.Plugin.Polyglot.Models;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

// Aliases for log entity types
using LogUserEntity = Jellyfin.Plugin.Polyglot.Models.LogUser;
using LogAlternativeEntity = Jellyfin.Plugin.Polyglot.Models.LogAlternative;

namespace Jellyfin.Plugin.Polyglot.Services;

/// <summary>
/// Service for managing user language assignments.
/// Uses IConfigurationService for all config modifications to prevent stale reference bugs.
/// </summary>
public class UserLanguageService : IUserLanguageService
{
    private readonly IUserManager _userManager;
    private readonly ILibraryAccessService _libraryAccessService;
    private readonly IConfigurationService _configService;
    private readonly ILogger<UserLanguageService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserLanguageService"/> class.
    /// </summary>
    public UserLanguageService(
        IUserManager userManager,
        ILibraryAccessService libraryAccessService,
        IConfigurationService configService,
        ILogger<UserLanguageService> logger)
    {
        _userManager = userManager;
        _libraryAccessService = libraryAccessService;
        _configService = configService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AssignLanguageAsync(Guid userId, Guid? alternativeId, string setBy, bool manuallySet = false, bool isPluginManaged = true, CancellationToken cancellationToken = default)
    {
        _logger.PolyglotDebug("AssignLanguageAsync: Assigning language to user {0}",
            _userManager.CreateLogUser(userId));

        var user = _userManager.GetUserById(userId)?.ToPolyglotUser();
        if (user == null)
        {
            throw new ArgumentException($"User {userId} not found", nameof(userId));
        }

        var userEntity = new LogUserEntity(userId, user.Username);

        // Validate alternative exists if specified
        LanguageAlternative? alternative = null;
        if (alternativeId.HasValue)
        {
            alternative = _configService.Read(c => c.LanguageAlternatives.FirstOrDefault(a => a.Id == alternativeId.Value));
            if (alternative == null)
            {
                throw new ArgumentException($"Language alternative {alternativeId} not found", nameof(alternativeId));
            }
        }

        // Update or create user language config atomically
        _configService.Update(c =>
        {
            var userConfig = c.UserLanguages.FirstOrDefault(u => u.UserId == userId);
            if (userConfig == null)
            {
                userConfig = new UserLanguageConfig { UserId = userId };
                c.UserLanguages.Add(userConfig);
            }

            userConfig.SelectedAlternativeId = alternativeId;
            userConfig.ManuallySet = manuallySet;
            userConfig.IsPluginManaged = isPluginManaged;
            userConfig.SetAt = DateTime.UtcNow;
            userConfig.SetBy = setBy;
        });

        if (alternative != null)
        {
            _logger.PolyglotInfo(
                "AssignLanguageAsync: Assigned language {0} to user {1} (by: {2}, manual: {3}, managed: {4})",
                new LogAlternativeEntity(alternative.Id, alternative.Name, alternative.LanguageCode),
                userEntity,
                setBy,
                manuallySet,
                isPluginManaged);
        }
        else
        {
            _logger.PolyglotInfo(
                "AssignLanguageAsync: Assigned default language to user {0} (by: {1}, manual: {2}, managed: {3})",
                userEntity,
                setBy,
                manuallySet,
                isPluginManaged);
        }

        // Update user's library access (only if managed)
        if (isPluginManaged)
        {
            await _libraryAccessService.UpdateUserLibraryAccessAsync(userId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public IEnumerable<UserInfo> GetAllUsersWithLanguages()
    {
        var users = _userManager.Users;
        var (userLanguages, alternatives) = _configService.Read(c =>
            (c.UserLanguages.ToList(), c.LanguageAlternatives.ToList()));

        foreach (var rawUser in users)
        {
            var user = new PolyglotUser(rawUser);
            var userInfo = new UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                IsAdministrator = user.HasPermission(PolyglotPermissionKind.IsAdministrator)
            };

            var userConfig = userLanguages.FirstOrDefault(u => u.UserId == user.Id);
            if (userConfig != null)
            {
                userInfo.IsPluginManaged = userConfig.IsPluginManaged;
                userInfo.AssignedAlternativeId = userConfig.SelectedAlternativeId;
                userInfo.ManuallySet = userConfig.ManuallySet;
                userInfo.SetBy = userConfig.SetBy;
                userInfo.SetAt = userConfig.SetAt;

                if (userConfig.SelectedAlternativeId.HasValue)
                {
                    var alt = alternatives.FirstOrDefault(a => a.Id == userConfig.SelectedAlternativeId.Value);
                    userInfo.AssignedAlternativeName = alt?.Name;
                }
            }

            yield return userInfo;
        }
    }

    /// <inheritdoc />
    public void RemoveUser(Guid userId)
    {
        _logger.PolyglotDebug("RemoveUser: Removing language assignment for user {0}",
            _userManager.CreateLogUser(userId));

        var userEntity = _userManager.CreateLogUser(userId);

        var removed = _configService.Update(c =>
        {
            var count = c.UserLanguages.RemoveAll(u => u.UserId == userId);
            return count > 0;
        });

        if (removed)
        {
            _logger.PolyglotInfo("RemoveUser: Removed language assignment for deleted user {0}", userEntity);
        }
        else
        {
            _logger.PolyglotDebug("RemoveUser: No language assignment found for user {0}", userEntity);
        }
    }
}
