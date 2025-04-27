using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PlexMediaOrganizer.Configuration;
using PlexMediaOrganizer.Data;
using PlexMediaOrganizer.Data.Repositories;
using PlexMediaOrganizer.Services;
using PlexMediaOrganizer.WebInterface;
using System.IO;

namespace PlexMediaOrganizer;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IFileSystemWatcherFactory _watcherFactory;
    private readonly IMediaProcessor _mediaProcessor;
    private readonly ISchedulerService _schedulerService;
    private readonly IEmailService _emailService;
    private readonly IProcessingStatusTracker _statusTracker;
    private readonly PlexMediaOrganizerSettings _settings;
    private readonly DatabaseInitializer? _databaseInitializer;
    private FileSystemWatcher? _watcher;
    private readonly IServiceProvider _serviceProvider;

    public Worker(
        ILogger<Worker> logger,
        IFileSystemWatcherFactory watcherFactory,
        IMediaProcessor mediaProcessor,
        ISchedulerService schedulerService,
        IEmailService emailService,
        IProcessingStatusTracker statusTracker,
        IOptions<PlexMediaOrganizerSettings> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _watcherFactory = watcherFactory;
        _mediaProcessor = mediaProcessor;
        _schedulerService = schedulerService;
        _emailService = emailService;
        _statusTracker = statusTracker;
        _settings = options.Value;
        _serviceProvider = serviceProvider;

        // Initialize database initializer if database is enabled
        if (_settings.Database.Enabled)
        {
            _databaseInitializer = new DatabaseInitializer(
                serviceProvider,
                serviceProvider.GetRequiredService<ILogger<DatabaseInitializer>>());
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Plex Media Organizer Service starting at: {time}", DateTimeOffset.Now);
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            EnsureDirectoriesExist();

            // Only catalog existing files
            _logger.LogInformation("Cataloging files in {SourceDirectory}", _settings.SourceDirectory);
            await CatalogDirectoryAsync(_settings.SourceDirectory, stoppingToken);

            // Monitor for new files
            _watcher = _watcherFactory.CreateWatcher(_settings.SourceDirectory);
            _watcher.Created += async (sender, e) => await OnNewFileDetectedAsync(e.FullPath, stoppingToken);
            _watcher.EnableRaisingEvents = true;

            // Monitor destination folders for duplicates
            await MonitorDestinationFoldersAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Plex Media Organizer Service");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Plex Media Organizer Service stopping at: {time}", DateTimeOffset.Now);
        _watcher?.Dispose();
        return base.StopAsync(cancellationToken);
    }

    private async Task CatalogDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        foreach (var file in Directory.GetFiles(path))
        {
            await OnNewFileDetectedAsync(file, cancellationToken);
        }

        foreach (var dir in Directory.GetDirectories(path))
        {
            await CatalogDirectoryAsync(dir, cancellationToken);
        }
    }

    private async Task OnNewFileDetectedAsync(string path, CancellationToken cancellationToken)
    {
        if (path.StartsWith(_settings.IncompleteDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (!_settings.AllowedVideoExtensions.Contains(extension))
        {
            return;
        }

        // Add to pending files database
        using var scope = _serviceProvider.CreateScope();
        var pendingRepo = scope.ServiceProvider.GetRequiredService<IPendingFileRepository>();
        await pendingRepo.AddAsync(new PendingFile
        {
            FilePath = path,
            FileSize = new FileInfo(path).Length,
            DetectedDate = DateTime.UtcNow,
            Status = "Pending"
        }, cancellationToken);
    }

    private async Task MonitorDestinationFoldersAsync(CancellationToken cancellationToken)
    {
        // Scan destination folders periodically for potential duplicates
        while (!cancellationToken.IsCancellationRequested)
        {
            await _mediaProcessor.ScanForDuplicatesAsync(_settings.MoviesDestination, cancellationToken);
            await _mediaProcessor.ScanForDuplicatesAsync(_settings.TvShowsDestination, cancellationToken);
            await Task.Delay(TimeSpan.FromHours(1), cancellationToken);
        }
    }

    private void EnsureDirectoriesExist()
    {
        try
        {
            // Ensure source directory exists
            if (!Directory.Exists(_settings.SourceDirectory))
            {
                _logger.LogWarning("Source directory does not exist: {SourceDirectory}. Creating it.", _settings.SourceDirectory);
                Directory.CreateDirectory(_settings.SourceDirectory);
            }

            // Ensure incomplete directory exists
            if (!Directory.Exists(_settings.IncompleteDirectory))
            {
                _logger.LogWarning("Incomplete directory does not exist: {IncompleteDirectory}. Creating it.", _settings.IncompleteDirectory);
                Directory.CreateDirectory(_settings.IncompleteDirectory);
            }

            // Ensure movies destination directory exists
            if (!Directory.Exists(_settings.MoviesDestination))
            {
                _logger.LogWarning("Movies destination directory does not exist: {MoviesDestination}. Creating it.", _settings.MoviesDestination);
                Directory.CreateDirectory(_settings.MoviesDestination);
            }

            // Ensure TV shows destination directory exists
            if (!Directory.Exists(_settings.TvShowsDestination))
            {
                _logger.LogWarning("TV shows destination directory does not exist: {TvShowsDestination}. Creating it.", _settings.TvShowsDestination);
                Directory.CreateDirectory(_settings.TvShowsDestination);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring directories exist");
        }
    }
}
