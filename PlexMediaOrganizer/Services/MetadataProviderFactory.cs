using Microsoft.Extensions.Options;
using PlexMediaOrganizer.Configuration;

namespace PlexMediaOrganizer.Services;

public interface IMetadataProviderFactory : IMetadataService
{
}

public class MetadataProviderFactory : IMetadataProviderFactory
{
    private readonly ILogger<MetadataProviderFactory> _logger;
    private readonly TmdbMetadataService _tmdbService;
    private readonly TvdbMetadataService _tvdbService;
    private readonly PlexMediaOrganizerSettings _settings;

    public MetadataProviderFactory(
        ILogger<MetadataProviderFactory> logger,
        TmdbMetadataService tmdbService,
        TvdbMetadataService tvdbService,
        IOptions<PlexMediaOrganizerSettings> options)
    {
        _logger = logger;
        _tmdbService = tmdbService;
        _tvdbService = tvdbService;
        _settings = options.Value;
    }

    public async Task<MediaMetadata?> GetMovieMetadataAsync(string title, int? year = null, CancellationToken cancellationToken = default)
    {
        // Try TMDb first
        if (!string.IsNullOrEmpty(_settings.TmdbApiKey))
        {
            var metadata = await _tmdbService.GetMovieMetadataAsync(title, year, cancellationToken);
            if (metadata != null)
            {
                _logger.LogInformation("Found movie metadata for {Title} from TMDb", title);
                return metadata;
            }
        }

        // Fall back to TVDB if TMDb failed
        if (!string.IsNullOrEmpty(_settings.TvdbApiKey))
        {
            var metadata = await _tvdbService.GetMovieMetadataAsync(title, year, cancellationToken);
            if (metadata != null)
            {
                _logger.LogInformation("Found movie metadata for {Title} from TVDB", title);
                return metadata;
            }
        }

        _logger.LogWarning("Could not find movie metadata for {Title} from any provider", title);
        return null;
    }

    public async Task<MediaMetadata?> GetTvShowMetadataAsync(string title, int? year = null, CancellationToken cancellationToken = default)
    {
        // Try TVDB first for TV shows
        if (!string.IsNullOrEmpty(_settings.TvdbApiKey))
        {
            var metadata = await _tvdbService.GetTvShowMetadataAsync(title, year, cancellationToken);
            if (metadata != null)
            {
                _logger.LogInformation("Found TV show metadata for {Title} from TVDB", title);
                return metadata;
            }
        }

        // Fall back to TMDb if TVDB failed
        if (!string.IsNullOrEmpty(_settings.TmdbApiKey))
        {
            var metadata = await _tmdbService.GetTvShowMetadataAsync(title, year, cancellationToken);
            if (metadata != null)
            {
                _logger.LogInformation("Found TV show metadata for {Title} from TMDb", title);
                return metadata;
            }
        }

        _logger.LogWarning("Could not find TV show metadata for {Title} from any provider", title);
        return null;
    }

    public async Task<byte[]?> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        // Try TMDb service first
        var imageData = await _tmdbService.DownloadImageAsync(imageUrl, cancellationToken);
        if (imageData != null && imageData.Length > 0)
        {
            return imageData;
        }

        // Fall back to TVDB service
        return await _tvdbService.DownloadImageAsync(imageUrl, cancellationToken);
    }
}
