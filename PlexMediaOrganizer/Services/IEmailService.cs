namespace PlexMediaOrganizer.Services;

public interface IEmailService
{
    Task SendEmailAsync(string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default);
    Task SendProcessingSuccessEmailAsync(string title, string filePath, string destinationPath, CancellationToken cancellationToken = default);
    Task SendProcessingErrorEmailAsync(string title, string filePath, string errorMessage, CancellationToken cancellationToken = default);
    Task SendProcessingCompletionEmailAsync(int totalProcessed, int successCount, int errorCount, CancellationToken cancellationToken = default);
}
