namespace PlexMediaOrganizer.Services;

public interface IMetadataService
{
    Task<MediaMetadata?> GetMovieMetadataAsync(string title, int? year = null, CancellationToken cancellationToken = default);
    Task<MediaMetadata?> GetTvShowMetadataAsync(string title, int? year = null, CancellationToken cancellationToken = default);
    Task<byte[]?> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken = default);
}
