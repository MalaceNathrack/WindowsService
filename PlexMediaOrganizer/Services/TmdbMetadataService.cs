using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PlexMediaOrganizer.Configuration;
using System.Net.Http;
using System.Text;

namespace PlexMediaOrganizer.Services;

public class TmdbMetadataService : IMetadataService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TmdbMetadataService> _logger;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.themoviedb.org/3";
    private const string ImageBaseUrl = "https://image.tmdb.org/t/p/original";

    public TmdbMetadataService(
        IHttpClientFactory httpClientFactory,
        IOptions<PlexMediaOrganizerSettings> options,
        ILogger<TmdbMetadataService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
        _apiKey = options.Value.TmdbApiKey;
    }

    public async Task<MediaMetadata?> GetMovieMetadataAsync(string title, int? year = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new StringBuilder(title);
            if (year.HasValue)
            {
                query.Append($" {year}");
            }

            var searchUrl = $"{BaseUrl}/search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(query.ToString())}";
            var response = await _httpClient.GetStringAsync(searchUrl, cancellationToken);
            var searchResult = JsonConvert.DeserializeObject<TmdbSearchResult>(response);

            if (searchResult?.Results == null || !searchResult.Results.Any())
            {
                _logger.LogWarning("No movie results found for {Title}", title);
                return null;
            }

            var movie = searchResult.Results.First();
            var detailsUrl = $"{BaseUrl}/movie/{movie.Id}?api_key={_apiKey}&append_to_response=credits,keywords";
            var detailsResponse = await _httpClient.GetStringAsync(detailsUrl, cancellationToken);
            var movieDetails = JsonConvert.DeserializeObject<TmdbMovieDetails>(detailsResponse);

            if (movieDetails == null)
            {
                _logger.LogWarning("Failed to get movie details for {Title}", title);
                return null;
            }

            return new MediaMetadata
            {
                Title = movieDetails.Title,
                Year = movieDetails.ReleaseDate?.Year,
                Overview = movieDetails.Overview,
                PosterUrl = !string.IsNullOrEmpty(movieDetails.PosterPath) 
                    ? $"{ImageBaseUrl}{movieDetails.PosterPath}" 
                    : null,
                BackdropUrl = !string.IsNullOrEmpty(movieDetails.BackdropPath) 
                    ? $"{ImageBaseUrl}{movieDetails.BackdropPath}" 
                    : null,
                ImdbId = movieDetails.ImdbId,
                TmdbId = movieDetails.Id,
                Type = MediaType.Movie,
                Genres = movieDetails.Genres?.Select(g => g.Name).ToList() ?? new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting movie metadata for {Title}", title);
            return null;
        }
    }

    public async Task<MediaMetadata?> GetTvShowMetadataAsync(string title, int? year = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new StringBuilder(title);
            if (year.HasValue)
            {
                query.Append($" {year}");
            }

            var searchUrl = $"{BaseUrl}/search/tv?api_key={_apiKey}&query={Uri.EscapeDataString(query.ToString())}";
            var response = await _httpClient.GetStringAsync(searchUrl, cancellationToken);
            var searchResult = JsonConvert.DeserializeObject<TmdbSearchResult>(response);

            if (searchResult?.Results == null || !searchResult.Results.Any())
            {
                _logger.LogWarning("No TV show results found for {Title}", title);
                return null;
            }

            var show = searchResult.Results.First();
            var detailsUrl = $"{BaseUrl}/tv/{show.Id}?api_key={_apiKey}&append_to_response=credits,keywords";
            var detailsResponse = await _httpClient.GetStringAsync(detailsUrl, cancellationToken);
            var showDetails = JsonConvert.DeserializeObject<TmdbTvShowDetails>(detailsResponse);

            if (showDetails == null)
            {
                _logger.LogWarning("Failed to get TV show details for {Title}", title);
                return null;
            }

            return new MediaMetadata
            {
                Title = showDetails.Name,
                Year = showDetails.FirstAirDate?.Year,
                Overview = showDetails.Overview,
                PosterUrl = !string.IsNullOrEmpty(showDetails.PosterPath) 
                    ? $"{ImageBaseUrl}{showDetails.PosterPath}" 
                    : null,
                BackdropUrl = !string.IsNullOrEmpty(showDetails.BackdropPath) 
                    ? $"{ImageBaseUrl}{showDetails.BackdropPath}" 
                    : null,
                TmdbId = showDetails.Id,
                Type = MediaType.TvShow,
                Genres = showDetails.Genres?.Select(g => g.Name).ToList() ?? new List<string>(),
                SeasonCount = showDetails.NumberOfSeasons,
                EpisodeCount = showDetails.NumberOfEpisodes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TV show metadata for {Title}", title);
            return null;
        }
    }

    public async Task<byte[]?> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image from {Url}", imageUrl);
            return null;
        }
    }

    // TMDB API response classes
    private class TmdbSearchResult
    {
        [JsonProperty("results")]
        public List<TmdbSearchItem>? Results { get; set; }
    }

    private class TmdbSearchItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("release_date")]
        public DateTime? ReleaseDate { get; set; }

        [JsonProperty("first_air_date")]
        public DateTime? FirstAirDate { get; set; }
    }

    private class TmdbMovieDetails
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("imdb_id")]
        public string? ImdbId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("overview")]
        public string? Overview { get; set; }

        [JsonProperty("poster_path")]
        public string? PosterPath { get; set; }

        [JsonProperty("backdrop_path")]
        public string? BackdropPath { get; set; }

        [JsonProperty("release_date")]
        public DateTime? ReleaseDate { get; set; }

        [JsonProperty("genres")]
        public List<TmdbGenre>? Genres { get; set; }
    }

    private class TmdbTvShowDetails
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("overview")]
        public string? Overview { get; set; }

        [JsonProperty("poster_path")]
        public string? PosterPath { get; set; }

        [JsonProperty("backdrop_path")]
        public string? BackdropPath { get; set; }

        [JsonProperty("first_air_date")]
        public DateTime? FirstAirDate { get; set; }

        [JsonProperty("number_of_seasons")]
        public int? NumberOfSeasons { get; set; }

        [JsonProperty("number_of_episodes")]
        public int? NumberOfEpisodes { get; set; }

        [JsonProperty("genres")]
        public List<TmdbGenre>? Genres { get; set; }
    }

    private class TmdbGenre
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
    }
}
