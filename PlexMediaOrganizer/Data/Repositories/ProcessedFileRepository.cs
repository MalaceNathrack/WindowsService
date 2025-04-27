using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlexMediaOrganizer.Data.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PlexMediaOrganizer.Data.Repositories;

public class ProcessedFileRepository : IProcessedFileRepository
{
    private readonly MediaOrganizerDbContext _dbContext;
    private readonly ILogger<ProcessedFileRepository> _logger;

    public ProcessedFileRepository(MediaOrganizerDbContext dbContext, ILogger<ProcessedFileRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> HasFileBeenProcessedAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.ProcessedFiles
                .AnyAsync(f => f.SourcePath == sourcePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if file has been processed: {SourcePath}", sourcePath);
            return false;
        }
    }

    public async Task<bool> FileHashExistsAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.ProcessedFiles
                .AnyAsync(f => f.FileHash == fileHash, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if file hash exists: {FileHash}", fileHash);
            return false;
        }
    }

    public async Task<ProcessedFile?> GetBySourcePathAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.ProcessedFiles
                .Include(f => f.MediaItem)
                .FirstOrDefaultAsync(f => f.SourcePath == sourcePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting processed file by source path: {SourcePath}", sourcePath);
            return null;
        }
    }

    public async Task<ProcessedFile?> GetByHashAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.ProcessedFiles
                .Include(f => f.MediaItem)
                .FirstOrDefaultAsync(f => f.FileHash == fileHash, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting processed file by hash: {FileHash}", fileHash);
            return null;
        }
    }

    public async Task<ProcessedFile> AddAsync(ProcessedFile processedFile, CancellationToken cancellationToken = default)
    {
        try
        {
            _dbContext.ProcessedFiles.Add(processedFile);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return processedFile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding processed file: {SourcePath}", processedFile.SourcePath);
            throw;
        }
    }

    public async Task<ProcessedFile> UpdateAsync(ProcessedFile processedFile, CancellationToken cancellationToken = default)
    {
        try
        {
            _dbContext.ProcessedFiles.Update(processedFile);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return processedFile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating processed file: {SourcePath}", processedFile.SourcePath);
            throw;
        }
    }

    public async Task<MediaItem?> GetMediaItemAsync(string title, int? year, string mediaType, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbContext.MediaItems
                .Where(m => m.Title == title && m.MediaType == mediaType);

            if (year.HasValue)
            {
                query = query.Where(m => m.Year == year);
            }

            return await query.FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting media item: {Title} ({Year}) - {MediaType}", title, year, mediaType);
            return null;
        }
    }

    public async Task<MediaItem?> GetTvEpisodeAsync(string title, int? year, int season, int episode, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbContext.MediaItems
                .Where(m => m.Title == title && 
                           m.MediaType == "TvEpisode" && 
                           m.Season == season && 
                           m.Episode == episode);

            if (year.HasValue)
            {
                query = query.Where(m => m.Year == year);
            }

            return await query.FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TV episode: {Title} ({Year}) - S{Season:D2}E{Episode:D2}", 
                title, year, season, episode);
            return null;
        }
    }

    public async Task<MediaItem> AddMediaItemAsync(MediaItem mediaItem, CancellationToken cancellationToken = default)
    {
        try
        {
            mediaItem.DateAdded = DateTime.UtcNow;
            mediaItem.LastUpdated = DateTime.UtcNow;
            
            _dbContext.MediaItems.Add(mediaItem);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return mediaItem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding media item: {Title} ({Year}) - {MediaType}", 
                mediaItem.Title, mediaItem.Year, mediaItem.MediaType);
            throw;
        }
    }

    public async Task<MediaItem> UpdateMediaItemAsync(MediaItem mediaItem, CancellationToken cancellationToken = default)
    {
        try
        {
            mediaItem.LastUpdated = DateTime.UtcNow;
            
            _dbContext.MediaItems.Update(mediaItem);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return mediaItem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating media item: {Title} ({Year}) - {MediaType}", 
                mediaItem.Title, mediaItem.Year, mediaItem.MediaType);
            throw;
        }
    }
}
