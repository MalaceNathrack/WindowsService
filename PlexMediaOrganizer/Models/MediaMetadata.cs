namespace PlexMediaOrganizer;

public class MediaMetadata
{
    public string Title { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? Overview { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? ImdbId { get; set; }
    public int? TmdbId { get; set; }
    public MediaType Type { get; set; }
    public List<string> Genres { get; set; } = new List<string>();
    
    // TV Show specific properties
    public int? SeasonCount { get; set; }
    public int? EpisodeCount { get; set; }
    
    // Episode specific properties
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public string? EpisodeTitle { get; set; }
}

public enum MediaType
{
    Unknown,
    Movie,
    TvShow,
    TvEpisode
}
