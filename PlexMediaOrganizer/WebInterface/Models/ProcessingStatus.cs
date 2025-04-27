namespace PlexMediaOrganizer.WebInterface.Models;

public class ProcessingStatus
{
    public string FilePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public MediaType MediaType { get; set; } = MediaType.Unknown;
    public string? Title { get; set; }
    public int? Year { get; set; }
    public string? DestinationPath { get; set; }
}

public class ProcessingStatusSummary
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public int MoviesCount { get; set; }
    public int TvShowsCount { get; set; }
    public DateTime LastProcessedTime { get; set; }
    public List<ProcessingStatus> RecentItems { get; set; } = new List<ProcessingStatus>();
}
