using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PlexMediaOrganizer.Configuration;
using System.Text.Json;

namespace PlexMediaOrganizer.WebInterface.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly ILogger<ConfigController> _logger;
    private readonly IOptions<PlexMediaOrganizerSettings> _options;
    private readonly string _configFilePath;

    public ConfigController(
        ILogger<ConfigController> logger,
        IOptions<PlexMediaOrganizerSettings> options)
    {
        _logger = logger;
        _options = options;
        _configFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    [HttpPut("update")]
    public async Task<IActionResult> UpdateConfig([FromBody] UpdateConfigRequest request)
    {
        try
        {
            // Read the current config file
            var configJson = await System.IO.File.ReadAllTextAsync(_configFilePath);
            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson);

            if (config != null && config.TryGetValue("PlexMediaOrganizerSettings", out var settingsElement))
            {
                // Convert to a mutable object
                var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(settingsElement.GetRawText());

                if (settings != null)
                {
                    // Update the settings with the new values
                    if (!string.IsNullOrEmpty(request.SourceDirectory))
                    {
                        settings["SourceDirectory"] = request.SourceDirectory;
                    }

                    if (!string.IsNullOrEmpty(request.IncompleteDirectory))
                    {
                        settings["IncompleteDirectory"] = request.IncompleteDirectory;
                    }

                    if (!string.IsNullOrEmpty(request.MoviesDestination))
                    {
                        settings["MoviesDestination"] = request.MoviesDestination;
                    }

                    if (!string.IsNullOrEmpty(request.TvShowsDestination))
                    {
                        settings["TvShowsDestination"] = request.TvShowsDestination;
                    }

                    if (!string.IsNullOrEmpty(request.TmdbApiKey))
                    {
                        settings["TmdbApiKey"] = request.TmdbApiKey;
                    }

                    if (request.ImageMaxWidth.HasValue)
                    {
                        settings["ImageMaxWidth"] = request.ImageMaxWidth.Value;
                    }

                    if (request.ImageMaxHeight.HasValue)
                    {
                        settings["ImageMaxHeight"] = request.ImageMaxHeight.Value;
                    }

                    // Update the config
                    config["PlexMediaOrganizerSettings"] = JsonSerializer.Deserialize<JsonElement>(
                        JsonSerializer.Serialize(settings));

                    // Write the updated config back to the file
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var updatedJson = JsonSerializer.Serialize(config, options);
                    await System.IO.File.WriteAllTextAsync(_configFilePath, updatedJson);
                }
            }

            _logger.LogInformation("Configuration updated successfully");

            return Ok(new { message = "Configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration");
            return StatusCode(500, new { error = "An error occurred while updating the configuration" });
        }
    }

    public class UpdateConfigRequest
    {
        public string? SourceDirectory { get; set; }
        public string? IncompleteDirectory { get; set; }
        public string? MoviesDestination { get; set; }
        public string? TvShowsDestination { get; set; }
        public string? TmdbApiKey { get; set; }
        public int? ImageMaxWidth { get; set; }
        public int? ImageMaxHeight { get; set; }
    }
}
