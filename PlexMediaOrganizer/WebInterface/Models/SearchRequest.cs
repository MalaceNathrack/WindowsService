namespace PlexMediaOrganizer.WebInterface.Models
{
    public class SearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
    }
}