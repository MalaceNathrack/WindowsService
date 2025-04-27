using System;
using System.Collections.Generic;

namespace PlexMediaOrganizer.Data.Entities;

/// <summary>
/// Represents a media item (movie or TV show)
/// </summary>
public class MediaItem
{
    public int Id { get; set; }
    
    /// <summary>
    /// Title of the media item
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Year of release
    /// </summary>
    public int? Year { get; set; }
    
    /// <summary>
    /// Type of media (Movie, TvShow, TvEpisode)
    /// </summary>
    public string MediaType { get; set; } = string.Empty;
    
    /// <summary>
    /// TMDB ID if available
    /// </summary>
    public int? TmdbId { get; set; }
    
    /// <summary>
    /// IMDB ID if available
    /// </summary>
    public string? ImdbId { get; set; }
    
    /// <summary>
    /// TVDB ID if available
    /// </summary>
    public int? TvdbId { get; set; }
    
    /// <summary>
    /// Season number (for TV episodes)
    /// </summary>
    public int? Season { get; set; }
    
    /// <summary>
    /// Episode number (for TV episodes)
    /// </summary>
    public int? Episode { get; set; }
    
    /// <summary>
    /// Episode title (for TV episodes)
    /// </summary>
    public string? EpisodeTitle { get; set; }
    
    /// <summary>
    /// When the item was added to the database
    /// </summary>
    public DateTime DateAdded { get; set; }
    
    /// <summary>
    /// When the item was last updated
    /// </summary>
    public DateTime LastUpdated { get; set; }
    
    /// <summary>
    /// Collection of files associated with this media item
    /// </summary>
    public ICollection<ProcessedFile> Files { get; set; } = new List<ProcessedFile>();
}
