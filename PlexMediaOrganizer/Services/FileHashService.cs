using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace PlexMediaOrganizer.Services;

public class FileHashService : IFileHashService
{
    private readonly ILogger<FileHashService> _logger;

    public FileHashService(ILogger<FileHashService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var md5 = MD5.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            
            var hashBytes = await md5.ComputeHashAsync(stream, cancellationToken);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating hash for file: {FilePath}", filePath);
            throw;
        }
    }

    public long GetFileSize(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file size: {FilePath}", filePath);
            throw;
        }
    }
}
