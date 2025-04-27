using System;

namespace PlexMediaOrganizer.Data.Entities;

/// <summary>
/// Represents a file that has been processed by the media organizer
/// </summary>
public class ProcessedFile
{
    public int Id { get; set; }
    
    /// <summary>
    /// Original file path that was processed
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Destination path where the file was copied to
    /// </summary>
    public string DestinationPath { get; set; } = string.Empty;
    
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }
    
    /// <summary>
    /// MD5 hash of the file content for duplicate detection
    /// </summary>
    public string FileHash { get; set; } = string.Empty;
    
    /// <summary>
    /// When the file was processed
    /// </summary>
    public DateTime ProcessedDate { get; set; }
    
    /// <summary>
    /// Status of the processing (Success, Error)
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Reference to the media item this file belongs to
    /// </summary>
    public int? MediaItemId { get; set; }
    public MediaItem? MediaItem { get; set; }
}
