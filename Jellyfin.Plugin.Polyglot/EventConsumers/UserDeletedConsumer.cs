using System.Threading.Tasks;
using Jellyfin.Data.Events.Users;
using Jellyfin.Plugin.Polyglot.Helpers;
using Jellyfin.Plugin.Polyglot.Models;
using Jellyfin.Plugin.Polyglot.Services;
using MediaBrowser.Controller.Events;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyglot.EventConsumers;

/// <summary>
/// Handles user deletion events to clean up language assignments.
/// </summary>
public class UserDeletedConsumer : IEventConsumer<UserDeletedEventArgs>
{
    private readonly IUserLanguageService _userLanguageService;
    private readonly ILogger<UserDeletedConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserDeletedConsumer"/> class.
    /// </summary>
    public UserDeletedConsumer(
        IUserLanguageService userLanguageService,
        ILogger<UserDeletedConsumer> logger)
    {
        _userLanguageService = userLanguageService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task OnEvent(UserDeletedEventArgs eventArgs)
    {
        var user = new PolyglotUser(eventArgs.Argument);
        var userEntity = new LogUser(user.Id, user.Username);
        _logger.PolyglotInfo("UserDeletedConsumer: User deleted: {0}", userEntity);

        // Remove user language assignment
        _userLanguageService.RemoveUser(user.Id);

        return Task.CompletedTask;
    }
}

