using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PlexMediaOrganizer.Configuration;
using PlexMediaOrganizer.WebInterface.Models;

namespace PlexMediaOrganizer.WebInterface.Controllers;

public class HomeController : Controller
{
    private readonly IProcessingStatusTracker _statusTracker;
    private readonly PlexMediaOrganizerSettings _settings;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IProcessingStatusTracker statusTracker,
        IOptions<PlexMediaOrganizerSettings> options,
        ILogger<HomeController> logger)
    {
        _statusTracker = statusTracker;
        _settings = options.Value;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var model = new HomeViewModel
        {
            StatusSummary = _statusTracker.GetStatusSummary(),
            Settings = new SettingsViewModel
            {
                SourceDirectory = _settings.SourceDirectory,
                IncompleteDirectory = _settings.IncompleteDirectory,
                MoviesDestination = _settings.MoviesDestination,
                TvShowsDestination = _settings.TvShowsDestination,
                HasTmdbApiKey = !string.IsNullOrEmpty(_settings.TmdbApiKey),
                HasTvdbApiKey = !string.IsNullOrEmpty(_settings.TvdbApiKey)
            }
        };

        return View(model);
    }
}
