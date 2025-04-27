using System.Threading;
using System.Threading.Tasks;

namespace PlexMediaOrganizer.Services;

public interface IFileHashService
{
    /// <summary>
    /// Calculates the MD5 hash of a file
    /// </summary>
    /// <param name="filePath">The path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The MD5 hash of the file as a hexadecimal string</returns>
    Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the size of a file in bytes
    /// </summary>
    /// <param name="filePath">The path to the file</param>
    /// <returns>The size of the file in bytes</returns>
    long GetFileSize(string filePath);
}
