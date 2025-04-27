using PlexMediaOrganizer.Data.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace PlexMediaOrganizer.Data.Repositories;

public interface IProcessedFileRepository
{
    /// <summary>
    /// Checks if a file with the given path has already been processed
    /// </summary>
    /// <param name="sourcePath">The source path of the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the file has been processed, false otherwise</returns>
    Task<bool> HasFileBeenProcessedAsync(string sourcePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a file with the given hash already exists in the database
    /// </summary>
    /// <param name="fileHash">The MD5 hash of the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if a file with the same hash exists, false otherwise</returns>
    Task<bool> FileHashExistsAsync(string fileHash, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a processed file by its source path
    /// </summary>
    /// <param name="sourcePath">The source path of the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The processed file or null if not found</returns>
    Task<ProcessedFile?> GetBySourcePathAsync(string sourcePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a processed file by its hash
    /// </summary>
    /// <param name="fileHash">The MD5 hash of the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The processed file or null if not found</returns>
    Task<ProcessedFile?> GetByHashAsync(string fileHash, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds a new processed file to the database
    /// </summary>
    /// <param name="processedFile">The processed file to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added processed file</returns>
    Task<ProcessedFile> AddAsync(ProcessedFile processedFile, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing processed file in the database
    /// </summary>
    /// <param name="processedFile">The processed file to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated processed file</returns>
    Task<ProcessedFile> UpdateAsync(ProcessedFile processedFile, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a media item by its title and year
    /// </summary>
    /// <param name="title">The title of the media item</param>
    /// <param name="year">The year of the media item (optional)</param>
    /// <param name="mediaType">The type of media (Movie, TvShow, TvEpisode)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The media item or null if not found</returns>
    Task<MediaItem?> GetMediaItemAsync(string title, int? year, string mediaType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a TV episode by its show title, season, and episode number
    /// </summary>
    /// <param name="title">The title of the TV show</param>
    /// <param name="year">The year of the TV show (optional)</param>
    /// <param name="season">The season number</param>
    /// <param name="episode">The episode number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The TV episode or null if not found</returns>
    Task<MediaItem?> GetTvEpisodeAsync(string title, int? year, int season, int episode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds a new media item to the database
    /// </summary>
    /// <param name="mediaItem">The media item to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added media item</returns>
    Task<MediaItem> AddMediaItemAsync(MediaItem mediaItem, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing media item in the database
    /// </summary>
    /// <param name="mediaItem">The media item to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated media item</returns>
    Task<MediaItem> UpdateMediaItemAsync(MediaItem mediaItem, CancellationToken cancellationToken = default);
}
