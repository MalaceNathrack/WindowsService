# Plex Media Organizer

A Windows Service that automatically organizes media files for Plex by:
- Monitoring a download directory for new files
- Parsing filenames to extract title, year, and episode information
- Retrieving metadata from TMDb and TVDB
- Downloading and optimizing poster images
- Organizing files according to Plex naming conventions
- Moving files to the appropriate destination folders

## Features

- Automatic detection of movies vs. TV shows
- Proper Plex-compatible naming and folder structure
- Metadata retrieval from TMDb and TVDB
- Poster image downloading and optimization
- Support for related files (subtitles, etc.)
- Configurable source and destination directories
- Logging and error handling
- Web interface for monitoring and configuration
- Advanced filename parsing for various naming conventions
- Email notifications for processing status
- Scheduled processing with configurable schedules

## Requirements

- .NET 8.0 or higher
- Windows operating system
- TMDb API key (free from [themoviedb.org](https://www.themoviedb.org/settings/api))
- TVDB API key (optional, free from [thetvdb.com](https://thetvdb.com/api-information))

## Installation

1. Build the project:
   ```
   dotnet build --configuration Release
   ```

2. Install the Windows Service:
   ```
   sc create PlexMediaOrganizer binPath= "C:\path\to\PlexMediaOrganizer.exe"
   sc description PlexMediaOrganizer "Automatically organizes media files for Plex"
   sc config PlexMediaOrganizer start= auto
   ```

3. Edit the configuration in `appsettings.json`:
   - Set your TMDb API key
   - Configure source and destination directories
   - Adjust other settings as needed

4. Start the service:
   ```
   sc start PlexMediaOrganizer
   ```

## Configuration

The service is configured through the `appsettings.json` file:

```json
{
  "PlexMediaOrganizerSettings": {
    "SourceDirectory": "C:\\downloads",
    "IncompleteDirectory": "C:\\downloads\\incomplete",
    "MoviesDestination": "\\\\192.168.1.96\\Movies",
    "TvShowsDestination": "\\\\192.168.1.96\\Shows",
    "TmdbApiKey": "YOUR_TMDB_API_KEY_HERE",
    "TvdbApiKey": "YOUR_TVDB_API_KEY_HERE",
    "ImageMaxWidth": 1000,
    "ImageMaxHeight": 1500,
    "AllowedVideoExtensions": [ ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".m4v" ],
    "AllowedImageExtensions": [ ".jpg", ".jpeg", ".png" ],
    "AllowedSubtitleExtensions": [ ".srt", ".sub", ".idx", ".ass" ],
    "IgnoredExtensions": [ ".nfo", ".txt", ".db", ".ini", ".log", ".part", ".!ut" ],
    "Email": {
      "Enabled": false,
      "SmtpServer": "smtp.example.com",
      "SmtpPort": 587,
      "UseSsl": true,
      "Username": "your-email@example.com",
      "Password": "your-password",
      "FromEmail": "your-email@example.com",
      "FromName": "Plex Media Organizer",
      "ToEmails": [ "recipient@example.com" ],
      "NotifyOnSuccess": false,
      "NotifyOnError": true,
      "NotifyOnCompletion": true,
      "MaxEmailsPerHour": 10
    },
    "Scheduler": {
      "Enabled": false,
      "ProcessDirectoryCronExpression": "0 0 * * *"
    }
  }
}
```

### Web Interface

The service includes a web interface that can be accessed at http://localhost:5000 when the service is running. The web interface provides:

- Status monitoring of processed files
- Configuration management
- Scheduled task management

### Email Notifications

Email notifications can be configured to receive alerts when:

- Files are successfully processed
- Errors occur during processing
- Processing tasks complete

### Scheduled Processing

The service can be configured to run processing tasks on a schedule using cron expressions. For example:

- `0 0 * * *` - Run daily at midnight
- `0 */6 * * *` - Run every 6 hours
- `0 0 * * 0` - Run weekly on Sunday at midnight

## Logging

Logs are written to the Windows Event Log and can be viewed in the Event Viewer under the "Application" log source.

## Troubleshooting

- Ensure the service account has read/write permissions to all configured directories
- Check the Windows Event Log for error messages
- Verify your TMDb API key is valid
- Make sure network paths are accessible from the service account

## License

MIT
