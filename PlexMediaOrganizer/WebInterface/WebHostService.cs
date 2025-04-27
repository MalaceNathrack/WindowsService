using Microsoft.AspNetCore.Builder;

namespace PlexMediaOrganizer.WebInterface;

public class WebHostService : BackgroundService
{
    private readonly ILogger<WebHostService> _logger;
    private readonly WebApplication _webApp;

    public WebHostService(
        ILogger<WebHostService> logger,
        WebApplication webApp)
    {
        _logger = logger;
        _webApp = webApp;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Web interface starting at: {time}", DateTimeOffset.Now);
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting web server on http://localhost:5000");

            // Start the web application
            await _webApp.StartAsync(stoppingToken);

            _logger.LogInformation("Web server started successfully");

            // Keep the service running until cancellation is requested
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in web host service");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Web interface stopping at: {time}", DateTimeOffset.Now);

        // Stop the web application
        await _webApp.StopAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }
}
