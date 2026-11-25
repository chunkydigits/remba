using Remba.Model.Entities;

namespace Remba.Service.Entities;

public interface ITmdbWatchProviderService
{
    bool IsConfigured { get; }

    string DefaultRegion { get; }

    /// <summary>
    /// Resolve streaming availability for an IMDb id in the given territory.
    /// Returns null when TMDB is not configured, the title is not found, or the call fails.
    /// </summary>
    Task<WatchProvidersSnapshot?> GetByImdbIdAsync(
        string entityType,
        string imdbId,
        string? region = null,
        CancellationToken cancellationToken = default);
}
