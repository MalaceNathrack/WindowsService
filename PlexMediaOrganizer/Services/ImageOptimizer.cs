using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace PlexMediaOrganizer.Services;

public class ImageOptimizer : IImageOptimizer
{
    private readonly ILogger<ImageOptimizer> _logger;

    public ImageOptimizer(ILogger<ImageOptimizer> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> OptimizeImageAsync(byte[] imageData, int maxWidth, int maxHeight, CancellationToken cancellationToken = default)
    {
        try
        {
            using var image = Image.Load(imageData);
            
            // Only resize if the image is larger than the max dimensions
            if (image.Width > maxWidth || image.Height > maxHeight)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxWidth, maxHeight)
                }));
            }

            // Save as JPEG with 85% quality
            using var ms = new MemoryStream();
            await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 85 }, cancellationToken);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing image");
            return imageData; // Return original if optimization fails
        }
    }

    public async Task OptimizeImageFileAsync(string filePath, int maxWidth, int maxHeight, CancellationToken cancellationToken = default)
    {
        try
        {
            var imageData = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var optimizedData = await OptimizeImageAsync(imageData, maxWidth, maxHeight, cancellationToken);
            await File.WriteAllBytesAsync(filePath, optimizedData, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing image file {FilePath}", filePath);
        }
    }
}
