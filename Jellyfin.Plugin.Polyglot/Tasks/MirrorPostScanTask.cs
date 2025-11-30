using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Polyglot.Helpers;
using Jellyfin.Plugin.Polyglot.Models;
using static Jellyfin.Plugin.Polyglot.Helpers.ProgressExtensions;
using Jellyfin.Plugin.Polyglot.Services;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyglot.Tasks;

/// <summary>
/// Post-scan task that synchronizes mirrors after library scans complete.
/// Uses IConfigurationService for config access and IDs for mirror operations.
/// </summary>
public class MirrorPostScanTask : ILibraryPostScanTask
{
    private readonly IMirrorService _mirrorService;
    private readonly IConfigurationService _configService;
    private readonly ILogger<MirrorPostScanTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MirrorPostScanTask"/> class.
    /// </summary>
    public MirrorPostScanTask(
        IMirrorService mirrorService,
        IConfigurationService configService,
        ILogger<MirrorPostScanTask> logger)
    {
        _mirrorService = mirrorService;
        _configService = configService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
    {
        // Check if auto-sync after library scans is enabled
        var syncEnabled = _configService.Read(c => c.SyncMirrorsAfterLibraryScan);
        if (!syncEnabled)
        {
            _logger.PolyglotDebug("MirrorPostScanTask: Auto-sync disabled, skipping");
            return;
        }

        _logger.PolyglotInfo("MirrorPostScanTask: Library scan completed, syncing mirrors");

        // Get alternative IDs (not objects) for iteration - only sync alternatives with mirrors
        var alternativeIds = _configService.Read(c => c.LanguageAlternatives
            .Where(a => a.MirroredLibraries.Count > 0)
            .Select(a => a.Id)
            .ToList());

        if (alternativeIds.Count == 0)
        {
            _logger.PolyglotDebug("MirrorPostScanTask: No alternatives with mirrors to sync");
            return;
        }

        var totalAlternatives = alternativeIds.Count;
        var completedAlternatives = 0;

        foreach (var alternativeId in alternativeIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Get fresh alternative data for logging
            var alternative = _configService.Read(c => c.LanguageAlternatives.FirstOrDefault(a => a.Id == alternativeId));
            if (alternative == null)
            {
                completedAlternatives++;
                continue;
            }

            var alternativeEntity = new LogAlternative(alternativeId, alternative.Name, alternative.LanguageCode);
            _logger.PolyglotDebug("MirrorPostScanTask: Post-scan sync for alternative: {0}", alternativeEntity);

            var altProgress = new Progress<double>(p =>
            {
                var overallProgress = ((completedAlternatives * 100.0) + p) / totalAlternatives;
                progress.SafeReport(overallProgress);
            });

            try
            {
                // Use ID instead of object reference
                var result = await _mirrorService.SyncAllMirrorsAsync(alternativeId, altProgress, cancellationToken).ConfigureAwait(false);

                if (result.Status == SyncAllStatus.AlternativeNotFound)
                {
                    _logger.PolyglotWarning("MirrorPostScanTask: Alternative {0} was deleted during sync", alternativeEntity);
                }
                else if (result.MirrorsFailed > 0)
                {
                    _logger.PolyglotWarning("MirrorPostScanTask: Alternative {0} synced with {1} failures out of {2} mirrors",
                        alternativeEntity, result.MirrorsFailed, result.TotalMirrors);
                }
            }
            catch (Exception ex)
            {
                _logger.PolyglotError(ex, "MirrorPostScanTask: Failed to sync alternative: {0}", alternativeEntity);
            }

            completedAlternatives++;
        }

        progress.SafeReport(100);
        _logger.PolyglotInfo("MirrorPostScanTask: Completed");
    }
}
