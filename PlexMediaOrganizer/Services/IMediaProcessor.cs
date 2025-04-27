namespace PlexMediaOrganizer.Services;

public interface IMediaProcessor
{
    Task ProcessFileAsync(string filePath, CancellationToken cancellationToken);
    Task ProcessDirectoryAsync(string directoryPath, CancellationToken cancellationToken);
    Task ProcessApprovedMovieAsync(string filePath, string title, int year, CancellationToken cancellationToken);
    Task ProcessApprovedTvEpisodeAsync(string filePath, string title, int year, int season, int episode, CancellationToken cancellationToken);
    Task ScanForDuplicatesAsync(string directoryPath, CancellationToken cancellationToken);
}

