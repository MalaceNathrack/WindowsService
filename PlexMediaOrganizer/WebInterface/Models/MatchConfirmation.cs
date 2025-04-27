namespace PlexMediaOrganizer.WebInterface.Models
{
    public class MatchConfirmation
    {
        public int PendingFileId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Year { get; set; }
        public string MediaType { get; set; } = string.Empty;
        public int? Season { get; set; }
        public int? Episode { get; set; }
        public string? TmdbId { get; set; }
    }
}