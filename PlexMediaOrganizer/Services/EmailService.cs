using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using PlexMediaOrganizer.Configuration;
using System.Collections.Concurrent;

namespace PlexMediaOrganizer.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly EmailSettings _emailSettings;
    private readonly ConcurrentQueue<DateTime> _emailSentTimes = new();
    private readonly SemaphoreSlim _emailSemaphore = new(1, 1);

    public EmailService(
        ILogger<EmailService> logger,
        IOptions<PlexMediaOrganizerSettings> options)
    {
        _logger = logger;
        _emailSettings = options.Value.Email;
    }

    public async Task SendEmailAsync(string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        if (!_emailSettings.Enabled)
        {
            _logger.LogDebug("Email notifications are disabled");
            return;
        }

        if (string.IsNullOrEmpty(_emailSettings.SmtpServer) || 
            string.IsNullOrEmpty(_emailSettings.Username) || 
            string.IsNullOrEmpty(_emailSettings.Password) ||
            string.IsNullOrEmpty(_emailSettings.FromEmail) ||
            _emailSettings.ToEmails.Length == 0)
        {
            _logger.LogWarning("Email settings are incomplete");
            return;
        }

        // Check rate limiting
        await _emailSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Clean up old timestamps
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            while (_emailSentTimes.TryPeek(out var oldestTime) && oldestTime < oneHourAgo)
            {
                _emailSentTimes.TryDequeue(out _);
            }

            // Check if we've sent too many emails in the last hour
            if (_emailSentTimes.Count >= _emailSettings.MaxEmailsPerHour)
            {
                _logger.LogWarning("Email rate limit reached ({Count}/{Max} emails per hour)", 
                    _emailSentTimes.Count, _emailSettings.MaxEmailsPerHour);
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromEmail));
            
            foreach (var toEmail in _emailSettings.ToEmails)
            {
                message.To.Add(new MailboxAddress("", toEmail));
            }
            
            message.Subject = subject;
            
            var bodyBuilder = new BodyBuilder();
            if (isHtml)
            {
                bodyBuilder.HtmlBody = body;
            }
            else
            {
                bodyBuilder.TextBody = body;
            }
            
            message.Body = bodyBuilder.ToMessageBody();
            
            using var client = new SmtpClient();
            await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, _emailSettings.UseSsl, cancellationToken);
            await client.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            
            // Record that we sent an email
            _emailSentTimes.Enqueue(DateTime.UtcNow);
            
            _logger.LogInformation("Email sent: {Subject}", subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email: {Subject}", subject);
        }
        finally
        {
            _emailSemaphore.Release();
        }
    }

    public async Task SendProcessingSuccessEmailAsync(string title, string filePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (!_emailSettings.Enabled || !_emailSettings.NotifyOnSuccess)
        {
            return;
        }

        var subject = $"Plex Media Organizer: Successfully processed {title}";
        var body = $@"
            <h2>Media Processing Success</h2>
            <p>The following media file was successfully processed:</p>
            <ul>
                <li><strong>Title:</strong> {title}</li>
                <li><strong>Source:</strong> {filePath}</li>
                <li><strong>Destination:</strong> {destinationPath}</li>
                <li><strong>Time:</strong> {DateTime.Now}</li>
            </ul>
        ";

        await SendEmailAsync(subject, body, true, cancellationToken);
    }

    public async Task SendProcessingErrorEmailAsync(string title, string filePath, string errorMessage, CancellationToken cancellationToken = default)
    {
        if (!_emailSettings.Enabled || !_emailSettings.NotifyOnError)
        {
            return;
        }

        var subject = $"Plex Media Organizer: Error processing {title}";
        var body = $@"
            <h2>Media Processing Error</h2>
            <p>An error occurred while processing the following media file:</p>
            <ul>
                <li><strong>Title:</strong> {title}</li>
                <li><strong>Source:</strong> {filePath}</li>
                <li><strong>Error:</strong> {errorMessage}</li>
                <li><strong>Time:</strong> {DateTime.Now}</li>
            </ul>
        ";

        await SendEmailAsync(subject, body, true, cancellationToken);
    }

    public async Task SendProcessingCompletionEmailAsync(int totalProcessed, int successCount, int errorCount, CancellationToken cancellationToken = default)
    {
        if (!_emailSettings.Enabled || !_emailSettings.NotifyOnCompletion)
        {
            return;
        }

        var subject = $"Plex Media Organizer: Processing Completed";
        var body = $@"
            <h2>Media Processing Completed</h2>
            <p>The media processing task has completed with the following results:</p>
            <ul>
                <li><strong>Total Processed:</strong> {totalProcessed}</li>
                <li><strong>Successful:</strong> {successCount}</li>
                <li><strong>Errors:</strong> {errorCount}</li>
                <li><strong>Completion Time:</strong> {DateTime.Now}</li>
            </ul>
        ";

        await SendEmailAsync(subject, body, true, cancellationToken);
    }
}
