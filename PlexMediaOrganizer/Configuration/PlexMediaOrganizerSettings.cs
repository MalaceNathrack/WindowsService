namespace PlexMediaOrganizer.Configuration;

public class PlexMediaOrganizerSettings
{
    public string SourceDirectory { get; set; } = @"C:\downloads";
    public string IncompleteDirectory { get; set; } = @"C:\downloads\incomplete";
    public string MoviesDestination { get; set; } = @"C:\Media\Movies";
    public string TvShowsDestination { get; set; } = @"C:\Media\TV Shows";
    public string TmdbApiKey { get; set; } = string.Empty;
    public string? TvdbApiKey { get; set; }
    public int ImageMaxWidth { get; set; } = 1000;
    public int ImageMaxHeight { get; set; } = 1500;
    public string[] AllowedVideoExtensions { get; set; } = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".m4v" };
    public string[] AllowedImageExtensions { get; set; } = new[] { ".jpg", ".jpeg", ".png" };
    public string[] AllowedSubtitleExtensions { get; set; } = new[] { ".srt", ".sub", ".idx", ".ass" };
    public string[] IgnoredExtensions { get; set; } = new[] { ".nfo", ".txt", ".db", ".ini", ".log", ".part", ".!ut" };

    // Email settings
    public EmailSettings Email { get; set; } = new EmailSettings();

    // Scheduler settings
    public SchedulerSettings Scheduler { get; set; } = new SchedulerSettings();

    // Database settings
    public DatabaseSettings Database { get; set; } = new DatabaseSettings();
}

public class EmailSettings
{
    public bool Enabled { get; set; } = false;
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Plex Media Organizer";
    public string[] ToEmails { get; set; } = Array.Empty<string>();
    public bool NotifyOnSuccess { get; set; } = false;
    public bool NotifyOnError { get; set; } = true;
    public bool NotifyOnCompletion { get; set; } = true;
    public int MaxEmailsPerHour { get; set; } = 10;
}

public class SchedulerSettings
{
    public bool Enabled { get; set; } = false;
    public string ProcessDirectoryCronExpression { get; set; } = "0 0 * * *"; // Daily at midnight
}

public class DatabaseSettings
{
    public bool Enabled { get; set; } = true;
    public string ConnectionString { get; set; } = "Data Source=PlexMediaOrganizer.db";
    public bool CheckForDuplicates { get; set; } = true;
    public bool SkipProcessedFiles { get; set; } = true;
}
