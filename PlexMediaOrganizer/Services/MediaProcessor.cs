using Microsoft.Extensions.Options;
using PlexMediaOrganizer.Configuration;
using PlexMediaOrganizer.Data.Entities;
using PlexMediaOrganizer.Data.Repositories;
using PlexMediaOrganizer.WebInterface;
using PlexMediaOrganizer.WebInterface.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace PlexMediaOrganizer.Services;

public class MediaProcessor : IMediaProcessor
{
    private readonly ILogger<MediaProcessor> _logger;
    private readonly IMetadataService _metadataService;
    private readonly IImageOptimizer _imageOptimizer;
    private readonly PlexMediaOrganizerSettings _settings;
    private readonly IEmailService _emailService;
    private readonly IProcessingStatusTracker _statusTracker;
    private readonly IServiceProvider _serviceProvider;
    private readonly IFileHashService _fileHashService;

    public MediaProcessor(
        ILogger<MediaProcessor> logger,
        IMetadataService metadataService,
        IImageOptimizer imageOptimizer,
        IEmailService emailService,
        IProcessingStatusTracker statusTracker,
        IServiceProvider serviceProvider,
        IFileHashService fileHashService,
        IOptions<PlexMediaOrganizerSettings> options)
    {
        _logger = logger;
        _metadataService = metadataService;
        _imageOptimizer = imageOptimizer;
        _emailService = emailService;
        _statusTracker = statusTracker;
        _serviceProvider = serviceProvider;
        _fileHashService = fileHashService;
        _settings = options.Value;
    }

    public async Task ProcessFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            // Skip files in the incomplete directory
            if (filePath.StartsWith(_settings.IncompleteDirectory, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Skipping file in incomplete directory: {FilePath}", filePath);
                return;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            // Skip ignored extensions
            if (_settings.IgnoredExtensions.Contains(extension))
            {
                _logger.LogDebug("Skipping file with ignored extension: {FilePath}", filePath);
                return;
            }

            // Check if the file has already been processed by path
            using (var scope = _serviceProvider.CreateScope())
            {
                var fileRepository = scope.ServiceProvider.GetRequiredService<IProcessedFileRepository>();
                if (await fileRepository.HasFileBeenProcessedAsync(filePath, cancellationToken))
                {
                    _logger.LogInformation("File has already been processed: {FilePath}", filePath);
                    return;
                }
            }

            // For video files, calculate hash and check for duplicates
            if (_settings.AllowedVideoExtensions.Contains(extension))
            {
                try
                {
                    // Calculate file hash for duplicate detection
                    var fileHash = await _fileHashService.CalculateFileHashAsync(filePath, cancellationToken);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var fileRepository = scope.ServiceProvider.GetRequiredService<IProcessedFileRepository>();

                        // Check if a file with the same hash has already been processed
                        var existingFile = await fileRepository.GetByHashAsync(fileHash, cancellationToken);
                        if (existingFile != null)
                        {
                            _logger.LogInformation(
                                "Duplicate file detected. File {FilePath} has the same hash as previously processed file {ExistingPath}",
                                filePath, existingFile.SourcePath);

                            // Add a record for this file pointing to the same media item
                            var processedFile = new ProcessedFile
                            {
                                SourcePath = filePath,
                                DestinationPath = existingFile.DestinationPath,
                                FileSize = _fileHashService.GetFileSize(filePath),
                                FileHash = fileHash,
                                ProcessedDate = DateTime.UtcNow,
                                Status = "Skipped - Duplicate",
                                MediaItemId = existingFile.MediaItemId
                            };

                            await fileRepository.AddAsync(processedFile, cancellationToken);
                            return;
                        }
                    }

                    // Process the video file
                    await ProcessVideoFileAsync(filePath, cancellationToken, fileHash);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating hash for file {FilePath}", filePath);
                    // Continue with processing without hash check
                    await ProcessVideoFileAsync(filePath, cancellationToken);
                }
            }
            // Process other files (subtitles, images, etc.) if needed
            else if (_settings.AllowedSubtitleExtensions.Contains(extension) ||
                     _settings.AllowedImageExtensions.Contains(extension))
            {
                // These will be handled when processing the video file
                _logger.LogDebug("Auxiliary file detected: {FilePath}", filePath);
            }
            else
            {
                _logger.LogInformation("Unsupported file type: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file {FilePath}", filePath);
        }
    }

    public async Task ProcessDirectoryAsync(string directoryPath, CancellationToken cancellationToken)
    {
        try
        {
            // Skip the incomplete directory
            if (directoryPath.Equals(_settings.IncompleteDirectory, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Skipping incomplete directory: {DirectoryPath}", directoryPath);
                return;
            }

            // Process all files in the directory
            foreach (var filePath in Directory.GetFiles(directoryPath))
            {
                await ProcessFileAsync(filePath, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                    break;
            }

            // Process subdirectories
            foreach (var subDirectory in Directory.GetDirectories(directoryPath))
            {
                await ProcessDirectoryAsync(subDirectory, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing directory {DirectoryPath}", directoryPath);
        }
    }

    private async Task ProcessVideoFileAsync(string filePath, CancellationToken cancellationToken, string? fileHash = null)
    {
        var fileName = Path.GetFileName(filePath);
        _logger.LogInformation("Processing video file: {FileName}", fileName);

        // Try to determine if it's a movie or TV show
        var (mediaType, title, year, season, episode) = ParseFileName(fileName);

        if (string.IsNullOrWhiteSpace(title))
        {
            _logger.LogWarning("Could not parse title from filename: {FileName}", fileName);
            return;
        }

        try
        {
            // Calculate file hash if not provided
            if (string.IsNullOrEmpty(fileHash))
            {
                try
                {
                    fileHash = await _fileHashService.CalculateFileHashAsync(filePath, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating hash for file {FilePath}", filePath);
                    // Continue without hash
                }
            }

            if (mediaType == MediaType.Movie)
            {
                await ProcessMovieAsync(filePath, title, year, cancellationToken, fileHash);
            }
            else if (mediaType == MediaType.TvEpisode)
            {
                await ProcessTvEpisodeAsync(filePath, title, year, season, episode, cancellationToken, fileHash);
            }
            else
            {
                _logger.LogWarning("Could not determine media type for: {FileName}", fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing video file {FilePath}", filePath);
        }
    }

    private async Task ProcessMovieAsync(string filePath, string title, int? year, CancellationToken cancellationToken, string? fileHash = null)
    {
        _logger.LogInformation("Processing movie: {Title} ({Year})", title, year);

        var status = new ProcessingStatus
        {
            FilePath = filePath,
            Status = "Processing",
            MediaType = MediaType.Movie,
            Title = title,
            Year = year,
            Timestamp = DateTime.Now
        };

        _statusTracker.AddProcessingStatus(status);

        try
        {
            // Get metadata from provider
            var metadata = await _metadataService.GetMovieMetadataAsync(title, year, cancellationToken);

            if (metadata == null)
            {
                var errorMessage = $"No metadata found for movie: {title} ({year})";
                _logger.LogWarning(errorMessage);

                status.Status = "Error";
                status.ErrorMessage = errorMessage;
                _statusTracker.AddProcessingStatus(status);

                // Save to database even if there was an error
                if (!string.IsNullOrEmpty(fileHash))
                {
                    var processedFile = new ProcessedFile
                    {
                        SourcePath = filePath,
                        DestinationPath = string.Empty,
                        FileSize = _fileHashService.GetFileSize(filePath),
                        FileHash = fileHash,
                        ProcessedDate = DateTime.UtcNow,
                        Status = "Error",
                        ErrorMessage = errorMessage
                    };

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var fileRepository = scope.ServiceProvider.GetRequiredService<IProcessedFileRepository>();
                        await fileRepository.AddAsync(processedFile, cancellationToken);
                    }
                }

                await _emailService.SendProcessingErrorEmailAsync(title, filePath, errorMessage, cancellationToken);
                return;
            }

            // Create Plex-compatible folder structure
            var movieTitle = metadata.Title;
            var movieYear = metadata.Year ?? year;
            var folderName = $"{movieTitle} ({movieYear})";
            var destinationFolder = Path.Combine(_settings.MoviesDestination, folderName);

            // Create the destination directory if it doesn't exist
            Directory.CreateDirectory(destinationFolder);

            // Copy the movie file with proper naming
            var destinationFileName = $"{folderName}{Path.GetExtension(filePath)}";
            var destinationPath = Path.Combine(destinationFolder, destinationFileName);

            await CopyFileAsync(filePath, destinationPath, cancellationToken);

            // Download and save poster if available
            if (!string.IsNullOrEmpty(metadata.PosterUrl))
            {
                await DownloadAndSavePosterAsync(metadata.PosterUrl, destinationFolder, cancellationToken);
            }

            // Copy any related files (subtitles, etc.)
            await CopyRelatedFilesAsync(filePath, destinationFolder, folderName, cancellationToken);

            _logger.LogInformation("Successfully processed movie: {Title}", metadata.Title);

            // Check if we already have this movie in the database
            MediaItem? mediaItem;
            using (var scope = _serviceProvider.CreateScope())
            {
                var fileRepository = scope.ServiceProvider.GetRequiredService<IProcessedFileRepository>();
                mediaItem = await fileRepository.GetMediaItemAsync(
                    metadata.Title,
                    metadata.Year,
                    "Movie",
                    cancellationToken);

                // If not, create a new media item
                if (mediaItem == null)
                {
                    mediaItem = new MediaItem
                    {
                        Title = metadata.Title,
                        Year = metadata.Year,
                        MediaType = "Movie",
                        TmdbId = metadata.TmdbId,
                        ImdbId = metadata.ImdbId,
                        DateAdded = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    mediaItem = await fileRepository.AddMediaItemAsync(mediaItem, cancellationToken);
                }
            }

            // Save the processed file to the database
            if (!string.IsNullOrEmpty(fileHash))
            {
                var processedFile = new ProcessedFile
                {
                    SourcePath = filePath,
                    DestinationPath = destinationPath,
                    FileSize = _fileHashService.GetFileSize(filePath),
                    FileHash = fileHash,
                    ProcessedDate = DateTime.UtcNow,
                    Status = "Success",
                    MediaItemId = mediaItem.Id
                };

                using (var scope = _serviceProvider.CreateScope())
                {
                    var fileRepository = scope.ServiceProvider.GetRequiredService<IProcessedFileRepository>();
                    await fileRepository.AddAsync(processedFile, cancellationToken);
                }
            }

            status.Status = "Success";
            status.Title = metadata.Title;
            status.Year = metadata.Year;
            status.DestinationPath = destinationPath;
            _statusTracker.AddProcessingStatus(status);

            await _emailService.SendProcessingSuccessEmailAsync(metadata.Title, filePath, destinationPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing movie: {Title} ({Year})", title, year);

            status.Status = "Error";
            status.ErrorMessage = ex.Message;
            _statusTracker.AddProcessingStatus(status);

            // Save to database even if there was an error
            if (!string.IsNullOrEmpty(fileHash))
            {
                var processedFile = new ProcessedFile
                {
                    SourcePath = filePath,
                    DestinationPath = string.Empty,
                    FileSize = _fileHashService.GetFileSize(filePath),
                    FileHash = fileHash,
                    ProcessedDate = DateTime.UtcNow,
                    Status = "Error",
                    ErrorMessage = ex.Message
                };

                using (var scope = _serviceProvider.CreateScope())
                {
                    var fileRepository = scope.ServiceProvider.GetRequiredService<IProcessedFileRepository>();
                    await fileRepository.AddAsync(processedFile, cancellationToken);
                }
            }

            await _emailService.SendProcessingErrorEmailAsync(title, filePath, ex.Message, cancellationToken);
        }
    }

    private async Task ProcessTvEpisodeAsync(string filePath, string title, int? year, int season, int episode, CancellationToken cancellationToken, string? fileHash = null)
    {
        _logger.LogInformation("Processing TV episode: {Title} ({Year}) - S{Season:D2}E{Episode:D2}", title, year, season, episode);

        var status = new ProcessingStatus
        {
            FilePath = filePath,
            Status = "Processing",
            MediaType = MediaType.TvEpisode,
            Title = title,
            Year = year,
            Timestamp = DateTime.Now
        };

        _statusTracker.AddProcessingStatus(status);

        try
        {
            // Get metadata from provider
            var metadata = await _metadataService.GetTvShowMetadataAsync(title, year, cancellationToken);

            if (metadata == null)
            {
                var errorMessage = $"No metadata found for TV show: {title} ({year})";
                _logger.LogWarning(errorMessage);

                status.Status = "Error";
                status.ErrorMessage = errorMessage;
                _statusTracker.AddProcessingStatus(status);

                // Save to database even if there was an error
                if (!string.IsNullOrEmpty(fileHash))
                {
                    var processedFile = new ProcessedFile
                    {
                        SourcePath = filePath,
                        DestinationPath = string.Empty,
                        FileSize = _fileHashService.GetFileSize(filePath),
                        FileHash = fileHash,
                        ProcessedDate = DateTime.UtcNow,
                        Status = "Error",
                        ErrorMessage = errorMessage
                    };

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var fileRepository = scope.ServiceProvider.GetRequiredService<IProcessedFileRepository>();
                        await fileRepository.AddAsync(processedFile, cancellationToken);
                    }
                }

                await _emailService.SendProcessingErrorEmailAsync(title, filePath, errorMessage, cancellationToken);
                return;
            }

            // Create Plex-compatible folder structure
            var showTitle = metadata.Title;
            var showYear = metadata.Year ?? year;
            var showFolderName = $"{showTitle} ({showYear})";
            var showFolder = Path.Combine(_settings.TvShowsDestination, showFolderName);
            var seasonFolder = Path.Combine(showFolder, $"Season {season:D2}");

            // Create the destination directories if they don't exist
            Directory.CreateDirectory(seasonFolder);

            // Copy the episode file with proper naming
            var destinationFileName = $"{showTitle} ({showYear}) - s{season:D2}e{episode:D2}{Path.GetExtension(filePath)}";
            var destinationPath = Path.Combine(seasonFolder, destinationFileName);

            await CopyFileAsync(filePath, destinationPath, cancellationToken);

            // Download and save show poster if available and not already present
            if (!string.IsNullOrEmpty(metadata.PosterUrl))
            {
                var posterPath = Path.Combine(showFolder, "poster.jpg");
                if (!File.Exists(posterPath))
                {
                    await DownloadAndSavePosterAsync(metadata.PosterUrl, showFolder, cancellationToken);
                }
            }

            // Copy any related files (subtitles, etc.)
            await CopyRelatedFilesAsync(filePath, seasonFolder, Path.GetFileNameWithoutExtension(destinationFileName), cancellationToken);

            _logger.LogInformation("Successfully processed TV episode: {Title} - S{Season:D2}E{Episode:D2}", metadata.Title, season, episode);

            // Check if we already have this TV show in the database
            MediaItem? tvShowItem;
            MediaItem? episodeItem;

            using (var scope = _serviceProvider.CreateScope())
            {
                var fileRepository = scope.ServiceProvider.GetRequiredService<IProcessedFileRepository>();

                tvShowItem = await fileRepository.GetMediaItemAsync(
                    metadata.Title,
                    metadata.Year,
                    "TvShow",
                    cancellationToken);

                // If not, create a new TV show item
                if (tvShowItem == null)
                {
                    tvShowItem = new MediaItem
                    {
                        Title = metadata.Title,
                        Year = metadata.Year,
                        MediaType = "TvShow",
                        TmdbId = metadata.TmdbId,
                        ImdbId = metadata.ImdbId,
                        TvdbId = metadata.TmdbId, // Use TMDB ID as TVDB ID if available
                        DateAdded = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    tvShowItem = await fileRepository.AddMediaItemAsync(tvShowItem, cancellationToken);
                }

                // Check if we already have this episode in the database
                episodeItem = await fileRepository.GetTvEpisodeAsync(
                    metadata.Title,
                    metadata.Year,
                    season,
                    episode,
                    cancellationToken);

                // If not, create a new episode item
                if (episodeItem == null)
                {
                    episodeItem = new MediaItem
                    {
                        Title = metadata.Title,
                        Year = metadata.Year,
                        MediaType = "TvEpisode",
                        TmdbId = metadata.TmdbId,
                        ImdbId = metadata.ImdbId,
                        Season = season,
                        Episode = episode,
                        DateAdded = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    episodeItem = await fileRepository.AddMediaItemAsync(episodeItem, cancellationToken);
                }
            }

            // Save the processed file to the database
            if (!string.IsNullOrEmpty(fileHash))
            {
                var processedFile = new ProcessedFile
                {
                    SourcePath = filePath,
                    DestinationPath = destinationPath,
                    FileSize = _fileHashService.GetFileSize(filePath),
                    FileHash = fileHash,
                    ProcessedDate = DateTime.UtcNow,
                    Status = "Success",
                    MediaItemId = episodeItem.Id
                };

                using (var scope = _serviceProvider.CreateScope())
                {
                    var fileRepository = scope.ServiceProvider.GetRequiredService<IProcessedFileRepository>();
                    await fileRepository.AddAsync(processedFile, cancellationToken);
                }
            }

            status.Status = "Success";
            status.Title = $"{metadata.Title} - S{season:D2}E{episode:D2}";
            status.Year = metadata.Year;
            status.DestinationPath = destinationPath;
            _statusTracker.AddProcessingStatus(status);

            await _emailService.SendProcessingSuccessEmailAsync($"{metadata.Title} - S{season:D2}E{episode:D2}", filePath, destinationPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing TV episode: {Title} ({Year}) - S{Season:D2}E{Episode:D2}", title, year, season, episode);

            status.Status = "Error";
            status.ErrorMessage = ex.Message;
            _statusTracker.AddProcessingStatus(status);

            // Save to database even if there was an error
            if (!string.IsNullOrEmpty(fileHash))
            {
                var processedFile = new ProcessedFile
                {
                    SourcePath = filePath,
                    DestinationPath = string.Empty,
                    FileSize = _fileHashService.GetFileSize(filePath),
                    FileHash = fileHash,
                    ProcessedDate = DateTime.UtcNow,
                    Status = "Error",
                    ErrorMessage = ex.Message
                };

                using (var scope = _serviceProvider.CreateScope())
                {
                    var fileRepository = scope.ServiceProvider.GetRequiredService<IProcessedFileRepository>();
                    await fileRepository.AddAsync(processedFile, cancellationToken);
                }
            }

            await _emailService.SendProcessingErrorEmailAsync($"{title} - S{season:D2}E{episode:D2}", filePath, ex.Message, cancellationToken);
        }
    }

    private async Task DownloadAndSavePosterAsync(string posterUrl, string destinationFolder, CancellationToken cancellationToken)
    {
        try
        {
            var imageData = await _metadataService.DownloadImageAsync(posterUrl, cancellationToken);

            if (imageData == null || imageData.Length == 0)
            {
                _logger.LogWarning("Failed to download poster from {Url}", posterUrl);
                return;
            }

            // Optimize the image
            var optimizedImageData = await _imageOptimizer.OptimizeImageAsync(
                imageData,
                _settings.ImageMaxWidth,
                _settings.ImageMaxHeight,
                cancellationToken);

            // Save as poster.jpg
            var posterPath = Path.Combine(destinationFolder, "poster.jpg");
            await File.WriteAllBytesAsync(posterPath, optimizedImageData, cancellationToken);

            _logger.LogInformation("Saved poster to {Path}", posterPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading and saving poster from {Url}", posterUrl);
        }
    }

    private async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        try
        {
            // Check if the destination file already exists
            if (File.Exists(destinationPath))
            {
                _logger.LogInformation("Destination file already exists: {DestinationPath}", destinationPath);
                return;
            }

            // Check if the destination directory exists, create if not
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            _logger.LogInformation("Copying file from {SourcePath} to {DestinationPath}", sourcePath, destinationPath);

            using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);

            await sourceStream.CopyToAsync(destinationStream, 81920, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying file from {SourcePath} to {DestinationPath}", sourcePath, destinationPath);
            throw; // Rethrow to handle in the calling method
        }
    }

    private async Task CopyRelatedFilesAsync(string videoFilePath, string destinationFolder, string baseFileName, CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(videoFilePath);
            if (string.IsNullOrEmpty(directory))
                return;

            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(videoFilePath);

            // Find related files (same name but different extensions)
            foreach (var file in Directory.GetFiles(directory))
            {
                // Skip the video file itself
                if (file.Equals(videoFilePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                var relatedFileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                var extension = Path.GetExtension(file).ToLowerInvariant();

                // Check if it's a related file (same name or starts with the same name)
                if (relatedFileNameWithoutExt.Equals(fileNameWithoutExt, StringComparison.OrdinalIgnoreCase) ||
                    relatedFileNameWithoutExt.StartsWith(fileNameWithoutExt, StringComparison.OrdinalIgnoreCase))
                {
                    // Check if it's a supported file type
                    if (_settings.AllowedSubtitleExtensions.Contains(extension) ||
                        _settings.AllowedImageExtensions.Contains(extension))
                    {
                        var destinationFileName = $"{baseFileName}{extension}";
                        var destinationPath = Path.Combine(destinationFolder, destinationFileName);

                        await CopyFileAsync(file, destinationPath, cancellationToken);

                        // Optimize images if needed
                        if (_settings.AllowedImageExtensions.Contains(extension))
                        {
                            await _imageOptimizer.OptimizeImageFileAsync(
                                destinationPath,
                                _settings.ImageMaxWidth,
                                _settings.ImageMaxHeight,
                                cancellationToken);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying related files for {VideoFilePath}", videoFilePath);
        }
    }

    private (MediaType Type, string Title, int? Year, int Season, int Episode) ParseFileName(string fileName)
    {
        // Use the advanced file parser
        return AdvancedFileParser.ParseFileName(fileName);
    }

    public async Task<string> ProcessApprovedMovieAsync(string filePath, string title, int? year, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing approved movie: {Title} ({Year})", title, year);

        var status = new ProcessingStatus
        {
            FilePath = filePath,
            Status = "Processing",
            MediaType = MediaType.Movie,
            Title = title,
            Year = year,
            Timestamp = DateTime.Now
        };

        _statusTracker.AddProcessingStatus(status);

        try
        {
            // Calculate file hash for duplicate detection
            string? fileHash = null;
            try
            {
                fileHash = await _fileHashService.CalculateFileHashAsync(filePath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating hash for file {FilePath}", filePath);
                // Continue without hash
            }

            // Get metadata from provider
            var metadata = await _metadataService.GetMovieMetadataAsync(title, year, cancellationToken);

            if (metadata == null)
            {
                var errorMessage = $"No metadata found for movie: {title} ({year})";
                _logger.LogWarning(errorMessage);

                status.Status = "Error";
                status.ErrorMessage = errorMessage;
                _statusTracker.AddProcessingStatus(status);

                throw new Exception(errorMessage);
            }

            // Create Plex-compatible folder structure
            var movieTitle = metadata.Title;
            var movieYear = metadata.Year ?? year;
            var folderName = $"{movieTitle} ({movieYear})";
            var destinationFolder = Path.Combine(_settings.MoviesDestination, folderName);

            // Create the destination directory if it doesn't exist
            Directory.CreateDirectory(destinationFolder);

            // Copy the movie file with proper naming
            var destinationFileName = $"{folderName}{Path.GetExtension(filePath)}";
            var destinationPath = Path.Combine(destinationFolder, destinationFileName);

            await CopyFileAsync(filePath, destinationPath, cancellationToken);

            // Download and save poster if available
            if (!string.IsNullOrEmpty(metadata.PosterUrl))
            {
                await DownloadAndSavePosterAsync(metadata.PosterUrl, destinationFolder, cancellationToken);
            }

            // Copy any related files (subtitles, etc.)
            await CopyRelatedFilesAsync(filePath, destinationFolder, folderName, cancellationToken);

            _logger.LogInformation("Successfully processed movie: {Title}", metadata.Title);

            // Check if we already have this movie in the database
            MediaItem? mediaItem;
            using (var scope = _serviceProvider.CreateScope())
            {
                var fileRepository = scope.ServiceProvider.GetRequiredService<IProcessedFileRepository>();
                mediaItem = await fileRepository.GetMediaItemAsync(
                    metadata.Title,
                    metadata.Year,
                    "Movie",
                    cancellationToken);

                // If not, create a new media item
                if (mediaItem == null)
                {
                    mediaItem = new MediaItem
                    {
                        Title = metadata.Title,
                        Year = metadata.Year,
                        MediaType = "Movie",
                        TmdbId = metadata.TmdbId,
                        ImdbId = metadata.ImdbId,
                        DateAdded = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    mediaItem = await fileRepository.AddMediaItemAsync(mediaItem, cancellationToken);
                }
            }

            // Save the processed file to the database
            if (!string.IsNullOrEmpty(fileHash))
            {
                var processedFile = new ProcessedFile
                {
                    SourcePath = filePath,
                    DestinationPath = destinationPath,
                    FileSize = _fileHashService.GetFileSize(filePath),
                    FileHash = fileHash,
                    ProcessedDate = DateTime.UtcNow,
                    Status = "Success",
                    MediaItemId = mediaItem.Id
                };

                using (var scope = _serviceProvider.CreateScope())
                {
                    var fileRepository = scope.ServiceProvider.GetRequiredService<IProcessedFileRepository>();
                    await fileRepository.AddAsync(processedFile, cancellationToken);
                }
            }

            status.Status = "Success";
            status.Title = metadata.Title;
            status.Year = metadata.Year;
            status.DestinationPath = destinationPath;
            _statusTracker.AddProcessingStatus(status);

            await _emailService.SendProcessingSuccessEmailAsync(metadata.Title, filePath, destinationPath, cancellationToken);

            // Delete the source file after successful processing
            try
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted source file after successful processing: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete source file after processing: {FilePath}", filePath);
            }

            return destinationPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing approved movie: {Title} ({Year})", title, year);

            status.Status = "Error";
            status.ErrorMessage = ex.Message;
            _statusTracker.AddProcessingStatus(status);

            await _emailService.SendProcessingErrorEmailAsync(title, filePath, ex.Message, cancellationToken);

            throw;
        }
    }

    public async Task<string> ProcessApprovedTvEpisodeAsync(string filePath, string title, int? year, int season, int episode, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing approved TV episode: {Title} ({Year}) - S{Season:D2}E{Episode:D2}", title, year, season, episode);

        var status = new ProcessingStatus
        {
            FilePath = filePath,
            Status = "Processing",
            MediaType = MediaType.TvEpisode,
            Title = title,
            Year = year,
            Timestamp = DateTime.Now
        };

        _statusTracker.AddProcessingStatus(status);

        try
        {
            // Calculate file hash for duplicate detection
            string? fileHash = null;
            try
            {
                fileHash = await _fileHashService.CalculateFileHashAsync(filePath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating hash for file {FilePath}", filePath);
                // Continue without hash
            }

            // Get metadata from provider
            var metadata = await _metadataService.GetTvShowMetadataAsync(title, year, cancellationToken);

            if (metadata == null)
            {
                var errorMessage = $"No metadata found for TV show: {title} ({year})";
                _logger.LogWarning(errorMessage);

                status.Status = "Error";
                status.ErrorMessage = errorMessage;
                _statusTracker.AddProcessingStatus(status);

                throw new Exception(errorMessage);
            }

            // Create Plex-compatible folder structure
            var showTitle = metadata.Title;
            var showYear = metadata.Year ?? year;
            var showFolderName = $"{showTitle} ({showYear})";
            var showFolder = Path.Combine(_settings.TvShowsDestination, showFolderName);
            var seasonFolder = Path.Combine(showFolder, $"Season {season:D2}");

            // Create the destination directories if they don't exist
            Directory.CreateDirectory(seasonFolder);

            // Copy the episode file with proper naming
            var destinationFileName = $"{showTitle} ({showYear}) - s{season:D2}e{episode:D2}{Path.GetExtension(filePath)}";
            var destinationPath = Path.Combine(seasonFolder, destinationFileName);

            await CopyFileAsync(filePath, destinationPath, cancellationToken);

            // Download and save show poster if available and not already present
            if (!string.IsNullOrEmpty(metadata.PosterUrl))
            {
                var posterPath = Path.Combine(showFolder, "poster.jpg");
                if (!File.Exists(posterPath))
                {
                    await DownloadAndSavePosterAsync(metadata.PosterUrl, showFolder, cancellationToken);
                }
            }

            // Copy any related files (subtitles, etc.)
            await CopyRelatedFilesAsync(filePath, seasonFolder, Path.GetFileNameWithoutExtension(destinationFileName), cancellationToken);

            _logger.LogInformation("Successfully processed TV episode: {Title} - S{Season:D2}E{Episode:D2}", metadata.Title, season, episode);

            // Check if we already have this TV show in the database
            MediaItem? tvShowItem;
            MediaItem? episodeItem;

            using (var scope = _serviceProvider.CreateScope())
            {
                var fileRepository = scope.ServiceProvider.GetRequiredService<IProcessedFileRepository>();

                tvShowItem = await fileRepository.GetMediaItemAsync(
                    metadata.Title,
                    metadata.Year,
                    "TvShow",
                    cancellationToken);

                // If not, create a new TV show item
                if (tvShowItem == null)
                {
                    tvShowItem = new MediaItem
                    {
                        Title = metadata.Title,
                        Year = metadata.Year,
                        MediaType = "TvShow",
                        TmdbId = metadata.TmdbId,
                        ImdbId = metadata.ImdbId,
                        TvdbId = metadata.TmdbId, // Use TMDB ID as TVDB ID if available
                        DateAdded = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    tvShowItem = await fileRepository.AddMediaItemAsync(tvShowItem, cancellationToken);
                }

                // Check if we already have this episode
                episodeItem = await fileRepository.GetTvEpisodeAsync(
                    metadata.Title,
                    metadata.Year,
                    season,
                    episode,
                    cancellationToken);

                // If not, create a new episode item
                if (episodeItem == null)
                {
                    episodeItem = new MediaItem
                    {
                        Title = $"{metadata.Title} - S{season:D2}E{episode:D2}",
                        Year = metadata.Year,
                        MediaType = "TvEpisode",
                        TmdbId = tvShowItem.TmdbId,
                        ImdbId = tvShowItem.ImdbId,
                        TvdbId = tvShowItem.TvdbId,
                        Season = season,
                        Episode = episode,
                        DateAdded = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    episodeItem = await fileRepository.AddMediaItemAsync(episodeItem, cancellationToken);
                }
            }

            // Save the processed file to the database
            if (!string.IsNullOrEmpty(fileHash))
            {
                var processedFile = new ProcessedFile
                {
                    SourcePath = filePath,
                    DestinationPath = destinationPath,
                    FileSize = _fileHashService.GetFileSize(filePath),
                    FileHash = fileHash,
                    ProcessedDate = DateTime.UtcNow,
                    Status = "Success",
                    MediaItemId = episodeItem.Id
                };

                using (var scope = _serviceProvider.CreateScope())
                {
                    var fileRepository = scope.ServiceProvider.GetRequiredService<IProcessedFileRepository>();
                    await fileRepository.AddAsync(processedFile, cancellationToken);
                }
            }

            status.Status = "Success";
            status.Title = $"{metadata.Title} - S{season:D2}E{episode:D2}";
            status.Year = metadata.Year;
            status.DestinationPath = destinationPath;
            _statusTracker.AddProcessingStatus(status);

            await _emailService.SendProcessingSuccessEmailAsync($"{metadata.Title} - S{season:D2}E{episode:D2}", filePath, destinationPath, cancellationToken);

            // Delete the source file after successful processing
            try
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted source file after successful processing: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete source file after processing: {FilePath}", filePath);
            }

            return destinationPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing approved TV episode: {Title} ({Year}) - S{Season:D2}E{Episode:D2}", title, year, season, episode);

            status.Status = "Error";
            status.ErrorMessage = ex.Message;
            _statusTracker.AddProcessingStatus(status);

            await _emailService.SendProcessingErrorEmailAsync($"{title} - S{season:D2}E{episode:D2}", filePath, ex.Message, cancellationToken);

            throw;
        }
    }
}
