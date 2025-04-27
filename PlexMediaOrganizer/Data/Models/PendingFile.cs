public class PendingFile
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime DetectedDate { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Matched, Rejected
    public string? SuggestedTitle { get; set; }
    public int? SuggestedYear { get; set; }
    public string? MediaType { get; set; } // Movie, TvShow
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public string? TmdbId { get; set; }
    public string? ReviewNotes { get; set; }
}