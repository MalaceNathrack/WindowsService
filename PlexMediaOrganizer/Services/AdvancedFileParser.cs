using System.Text.RegularExpressions;

namespace PlexMediaOrganizer.Services;

public class AdvancedFileParser
{
    // Common patterns for movie and TV show filenames
    private static readonly Regex[] MoviePatterns = new[]
    {
        // Standard movie pattern: Movie Title (2023).ext
        new Regex(@"^(.+?)[\.\s][\(\[]?(\d{4})[\)\]]?[\.\s]", RegexOptions.Compiled),
        
        // Movie with quality/resolution: Movie.Title.2023.1080p.BluRay.x264.ext
        new Regex(@"^(.+?)[\.\s](\d{4})[\.\s](?:(?:\d+p|UHD|HD|BluRay|WEB-DL|HDRip|BRRip|DVDRip|WEBRip)[\.\s])", RegexOptions.Compiled),
        
        // Movie with scene release format: Movie.Title.2023.Source.Quality.Codec-GROUP.ext
        new Regex(@"^(.+?)[\.\s](\d{4})[\.\s](?:(?:[a-zA-Z0-9]+\.)+)(?:[a-zA-Z0-9-]+)$", RegexOptions.Compiled),
        
        // Movie with dots instead of spaces: Movie.Title.2023.ext
        new Regex(@"^([a-zA-Z0-9\.]+)\.(\d{4})\.", RegexOptions.Compiled)
    };

    private static readonly Regex[] TvShowPatterns = new[]
    {
        // Standard TV pattern: Show Title (2023) S01E01.ext
        new Regex(@"^(.+?)[\.\s][\(\[]?(\d{4})[\)\]]?[\.\s][Ss](\d{1,2})[Ee](\d{1,2})", RegexOptions.Compiled),
        
        // TV show with dots: Show.Title.2023.S01E01.ext
        new Regex(@"^(.+?)[\.\s](\d{4})[\.\s][Ss](\d{1,2})[Ee](\d{1,2})", RegexOptions.Compiled),
        
        // TV show with season and episode numbers: Show.Title.S01E01.ext
        new Regex(@"^(.+?)[\.\s][Ss](\d{1,2})[Ee](\d{1,2})", RegexOptions.Compiled),
        
        // TV show with season and episode numbers separated: Show.Title.S01.E01.ext
        new Regex(@"^(.+?)[\.\s][Ss](\d{1,2})[\.\s][Ee](\d{1,2})", RegexOptions.Compiled),
        
        // TV show with just numbers: Show.Title.101.ext (season 1, episode 1)
        new Regex(@"^(.+?)[\.\s](\d)(\d{2})[\.\s]", RegexOptions.Compiled),
        
        // TV show with scene release format: Show.Title.S01E01.Source.Quality.Codec-GROUP.ext
        new Regex(@"^(.+?)[\.\s][Ss](\d{1,2})[Ee](\d{1,2})[\.\s](?:(?:[a-zA-Z0-9]+\.)+)(?:[a-zA-Z0-9-]+)$", RegexOptions.Compiled)
    };

    // Patterns for cleaning up titles
    private static readonly Regex[] CleanupPatterns = new[]
    {
        // Remove quality indicators
        new Regex(@"[\.\s](?:(?:720p|1080p|2160p|UHD|HD|BluRay|WEB-DL|HDRip|BRRip|DVDRip|WEBRip)[\.\s])", RegexOptions.Compiled),
        
        // Remove scene release groups
        new Regex(@"[\.\s]-[a-zA-Z0-9]+$", RegexOptions.Compiled),
        
        // Remove file extension
        new Regex(@"[\.\s][^\.\s]+$", RegexOptions.Compiled),
        
        // Remove extra dots and spaces
        new Regex(@"[\.\s]{2,}", RegexOptions.Compiled)
    };

    public static (MediaType Type, string Title, int? Year, int Season, int Episode) ParseFileName(string fileName)
    {
        // Try to match as TV show first
        foreach (var pattern in TvShowPatterns)
        {
            var match = pattern.Match(fileName);
            if (match.Success)
            {
                var title = CleanupTitle(match.Groups[1].Value);
                
                // Extract year if available (some patterns might not have year group)
                int? year = null;
                if (match.Groups.Count > 2 && int.TryParse(match.Groups[2].Value, out var y) && y >= 1900 && y <= 2100)
                {
                    year = y;
                }
                
                // Extract season and episode
                int season = 1;
                int episode = 1;
                
                // Different patterns have season/episode in different groups
                if (match.Groups.Count > 4)
                {
                    // Standard pattern with year: groups are title, year, season, episode
                    int.TryParse(match.Groups[3].Value, out season);
                    int.TryParse(match.Groups[4].Value, out episode);
                }
                else if (match.Groups.Count > 3)
                {
                    // Pattern without year: groups are title, season, episode
                    int.TryParse(match.Groups[2].Value, out season);
                    int.TryParse(match.Groups[3].Value, out episode);
                }
                else if (match.Groups.Count > 2)
                {
                    // Pattern with combined season/episode: groups are title, season, episode
                    int.TryParse(match.Groups[2].Value, out season);
                    int.TryParse(match.Groups[3].Value, out episode);
                }
                
                return (MediaType.TvEpisode, title, year, season, episode);
            }
        }
        
        // Try to match as movie
        foreach (var pattern in MoviePatterns)
        {
            var match = pattern.Match(fileName);
            if (match.Success)
            {
                var title = CleanupTitle(match.Groups[1].Value);
                
                // Extract year
                int? year = null;
                if (int.TryParse(match.Groups[2].Value, out var y) && y >= 1900 && y <= 2100)
                {
                    year = y;
                }
                
                return (MediaType.Movie, title, year, 0, 0);
            }
        }
        
        // Could not determine type
        return (MediaType.Unknown, Path.GetFileNameWithoutExtension(fileName), null, 0, 0);
    }

    public static string CleanupTitle(string title)
    {
        // Replace dots and underscores with spaces
        title = title.Replace('.', ' ').Replace('_', ' ');
        
        // Apply cleanup patterns
        foreach (var pattern in CleanupPatterns)
        {
            title = pattern.Replace(title, " ");
        }
        
        // Trim and normalize spaces
        title = title.Trim();
        while (title.Contains("  "))
        {
            title = title.Replace("  ", " ");
        }
        
        // Capitalize first letter of each word
        if (!string.IsNullOrEmpty(title))
        {
            var words = title.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (!string.IsNullOrEmpty(words[i]))
                {
                    words[i] = char.ToUpper(words[i][0]) + (words[i].Length > 1 ? words[i].Substring(1).ToLower() : "");
                }
            }
            title = string.Join(" ", words);
        }
        
        return title;
    }
}
