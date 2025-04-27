namespace PlexMediaOrganizer.Services;

public interface IImageOptimizer
{
    Task<byte[]> OptimizeImageAsync(byte[] imageData, int maxWidth, int maxHeight, CancellationToken cancellationToken = default);
    Task OptimizeImageFileAsync(string filePath, int maxWidth, int maxHeight, CancellationToken cancellationToken = default);
}
