using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PlexMediaOrganizer.Configuration;
using System.Net.Http;
using System.Text;

namespace PlexMediaOrganizer.Services;

public class TvdbMetadataService : IMetadataService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TvdbMetadataService> _logger;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.thetvdb.com/v4";
    private const string ImageBaseUrl = "https://artworks.thetvdb.com";
    private string? _authToken;
    private DateTime _tokenExpiration = DateTime.MinValue;
    private readonly SemaphoreSlim _authSemaphore = new(1, 1);

    public TvdbMetadataService(
        IHttpClientFactory httpClientFactory,
        IOptions<PlexMediaOrganizerSettings> options,
        ILogger<TvdbMetadataService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
        _apiKey = options.Value.TvdbApiKey ?? string.Empty;
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        // Check if we need to authenticate
        if (!string.IsNullOrEmpty(_authToken) && DateTime.UtcNow < _tokenExpiration)
        {
            return;
        }

        await _authSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring the semaphore
            if (!string.IsNullOrEmpty(_authToken) && DateTime.UtcNow < _tokenExpiration)
            {
                return;
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("TVDB API key is not configured");
                return;
            }

            var authRequest = new
            {
                apikey = _apiKey
            };

            var content = new StringContent(JsonConvert.SerializeObject(authRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/login", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var authResponse = JsonConvert.DeserializeObject<TvdbAuthResponse>(await response.Content.ReadAsStringAsync(cancellationToken));
                if (authResponse?.Data?.Token != null)
                {
                    _authToken = authResponse.Data.Token;
                    _tokenExpiration = DateTime.UtcNow.AddHours(23); // Token is valid for 24 hours, but we'll refresh a bit earlier
                    _logger.LogInformation("Successfully authenticated with TVDB API");
                }
                else
                {
                    _logger.LogWarning("Failed to parse TVDB authentication response");
                }
            }
            else
            {
                _logger.LogWarning("Failed to authenticate with TVDB API: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating with TVDB API");
        }
        finally
        {
            _authSemaphore.Release();
        }
    }

    public async Task<MediaMetadata?> GetMovieMetadataAsync(string title, int? year = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);
            if (string.IsNullOrEmpty(_authToken))
            {
                return null;
            }

            var query = new StringBuilder(title);
            if (year.HasValue)
            {
                query.Append($" {year}");
            }

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
            var response = await _httpClient.GetAsync($"{BaseUrl}/search?type=movie&query={Uri.EscapeDataString(query.ToString())}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var searchResult = JsonConvert.DeserializeObject<TvdbSearchResponse>(await response.Content.ReadAsStringAsync(cancellationToken));

                if (searchResult?.Data?.Count > 0)
                {
                    var movie = searchResult.Data[0];
                    var movieId = movie.Id;

                    var movieResponse = await _httpClient.GetAsync($"{BaseUrl}/movies/{movieId}/extended", cancellationToken);
                    if (movieResponse.IsSuccessStatusCode)
                    {
                        var movieDetails = JsonConvert.DeserializeObject<TvdbMovieResponse>(await movieResponse.Content.ReadAsStringAsync(cancellationToken));
                        if (movieDetails?.Data != null)
                        {
                            var metadata = new MediaMetadata
                            {
                                Title = movieDetails.Data.Name,
                                Year = movieDetails.Data.Year,
                                Overview = movieDetails.Data.Overview,
                                Type = MediaType.Movie,
                                ImdbId = movieDetails.Data.RemoteIds?.FirstOrDefault(r => r.SourceName == "IMDB")?.Id
                            };

                            if (movieDetails.Data.Image != null)
                            {
                                metadata.PosterUrl = $"{ImageBaseUrl}{movieDetails.Data.Image}";
                            }

                            if (movieDetails.Data.Artworks?.Count > 0)
                            {
                                var backdrop = movieDetails.Data.Artworks.FirstOrDefault(a => a.Type == "background");
                                if (backdrop != null)
                                {
                                    metadata.BackdropUrl = $"{ImageBaseUrl}{backdrop.Image}";
                                }
                            }

                            return metadata;
                        }
                    }
                }
            }

            _logger.LogWarning("No movie results found for {Title} in TVDB", title);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting movie metadata from TVDB for {Title}", title);
            return null;
        }
    }

    public async Task<MediaMetadata?> GetTvShowMetadataAsync(string title, int? year = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);
            if (string.IsNullOrEmpty(_authToken))
            {
                return null;
            }

            var query = new StringBuilder(title);
            if (year.HasValue)
            {
                query.Append($" {year}");
            }

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
            var response = await _httpClient.GetAsync($"{BaseUrl}/search?type=series&query={Uri.EscapeDataString(query.ToString())}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var searchResult = JsonConvert.DeserializeObject<TvdbSearchResponse>(await response.Content.ReadAsStringAsync(cancellationToken));

                if (searchResult?.Data?.Count > 0)
                {
                    var series = searchResult.Data[0];
                    var seriesId = series.Id;

                    var seriesResponse = await _httpClient.GetAsync($"{BaseUrl}/series/{seriesId}/extended", cancellationToken);
                    if (seriesResponse.IsSuccessStatusCode)
                    {
                        var seriesDetails = JsonConvert.DeserializeObject<TvdbSeriesResponse>(await seriesResponse.Content.ReadAsStringAsync(cancellationToken));
                        if (seriesDetails?.Data != null)
                        {
                            var metadata = new MediaMetadata
                            {
                                Title = seriesDetails.Data.Name,
                                Year = seriesDetails.Data.FirstAired?.Year,
                                Overview = seriesDetails.Data.Overview,
                                Type = MediaType.TvShow,
                                SeasonCount = seriesDetails.Data.Seasons?.Count(s => s.Type?.Equals("official", StringComparison.OrdinalIgnoreCase) == true)
                            };

                            if (seriesDetails.Data.Image != null)
                            {
                                metadata.PosterUrl = $"{ImageBaseUrl}{seriesDetails.Data.Image}";
                            }

                            if (seriesDetails.Data.Artworks?.Count > 0)
                            {
                                var backdrop = seriesDetails.Data.Artworks.FirstOrDefault(a => a.Type == "background");
                                if (backdrop != null)
                                {
                                    metadata.BackdropUrl = $"{ImageBaseUrl}{backdrop.Image}";
                                }
                            }

                            return metadata;
                        }
                    }
                }
            }

            _logger.LogWarning("No TV show results found for {Title} in TVDB", title);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TV show metadata from TVDB for {Title}", title);
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

    // TVDB API response classes
    private class TvdbAuthResponse
    {
        [JsonProperty("data")]
        public TvdbAuthData? Data { get; set; }
    }

    private class TvdbAuthData
    {
        [JsonProperty("token")]
        public string? Token { get; set; }
    }

    private class TvdbSearchResponse
    {
        [JsonProperty("data")]
        public List<TvdbSearchResult>? Data { get; set; }
    }

    private class TvdbSearchResult
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }
    }

    private class TvdbMovieResponse
    {
        [JsonProperty("data")]
        public TvdbMovieData? Data { get; set; }
    }

    private class TvdbMovieData
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("overview")]
        public string? Overview { get; set; }

        [JsonProperty("year")]
        public int? Year { get; set; }

        [JsonProperty("image")]
        public string? Image { get; set; }

        [JsonProperty("artworks")]
        public List<TvdbArtwork>? Artworks { get; set; }

        [JsonProperty("remoteIds")]
        public List<TvdbRemoteId>? RemoteIds { get; set; }
    }

    private class TvdbSeriesResponse
    {
        [JsonProperty("data")]
        public TvdbSeriesData? Data { get; set; }
    }

    private class TvdbSeriesData
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("overview")]
        public string? Overview { get; set; }

        [JsonProperty("firstAired")]
        public DateTime? FirstAired { get; set; }

        [JsonProperty("image")]
        public string? Image { get; set; }

        [JsonProperty("artworks")]
        public List<TvdbArtwork>? Artworks { get; set; }

        [JsonProperty("seasons")]
        public List<TvdbSeason>? Seasons { get; set; }
    }

    private class TvdbArtwork
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("image")]
        public string Image { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
    }

    private class TvdbRemoteId
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("sourceName")]
        public string SourceName { get; set; } = string.Empty;
    }

    private class TvdbSeason
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("number")]
        public int Number { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }
    }
}
