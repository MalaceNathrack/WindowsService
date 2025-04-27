using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PlexMediaOrganizer.Configuration;
using PlexMediaOrganizer.WebInterface.Models;

namespace PlexMediaOrganizer.WebInterface.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly IProcessingStatusTracker _statusTracker;
    private readonly ILogger<StatusController> _logger;
    private readonly PlexMediaOrganizerSettings _settings;

    public StatusController(
        IProcessingStatusTracker statusTracker,
        ILogger<StatusController> logger,
        IOptions<PlexMediaOrganizerSettings> options)
    {
        _statusTracker = statusTracker;
        _logger = logger;
        _settings = options.Value;
    }

    [HttpGet("summary")]
    public ActionResult<ProcessingStatusSummary> GetSummary()
    {
        try
        {
            return _statusTracker.GetStatusSummary();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status summary");
            return StatusCode(500, "An error occurred while retrieving the status summary");
        }
    }

    [HttpGet("recent")]
    public ActionResult<List<ProcessingStatus>> GetRecentItems([FromQuery] int count = 50)
    {
        try
        {
            return _statusTracker.GetRecentItems(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent items");
            return StatusCode(500, "An error occurred while retrieving recent items");
        }
    }

    [HttpGet("errors")]
    public ActionResult<List<ProcessingStatus>> GetErrorItems([FromQuery] int count = 50)
    {
        try
        {
            return _statusTracker.GetErrorItems(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting error items");
            return StatusCode(500, "An error occurred while retrieving error items");
        }
    }

    [HttpGet("settings")]
    public ActionResult<object> GetSettings()
    {
        try
        {
            // Return a sanitized version of the settings (without API keys)
            return new
            {
                _settings.SourceDirectory,
                _settings.IncompleteDirectory,
                _settings.MoviesDestination,
                _settings.TvShowsDestination,
                _settings.ImageMaxWidth,
                _settings.ImageMaxHeight,
                _settings.AllowedVideoExtensions,
                _settings.AllowedImageExtensions,
                _settings.AllowedSubtitleExtensions,
                _settings.IgnoredExtensions,
                HasTmdbApiKey = !string.IsNullOrEmpty(_settings.TmdbApiKey)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting settings");
            return StatusCode(500, "An error occurred while retrieving settings");
        }
    }
}
