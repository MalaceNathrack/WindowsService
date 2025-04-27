namespace PlexMediaOrganizer.WebInterface.Models;

public class HomeViewModel
{
    public ProcessingStatusSummary StatusSummary { get; set; } = new ProcessingStatusSummary();
    public SettingsViewModel Settings { get; set; } = new SettingsViewModel();
}

public class SettingsViewModel
{
    public string SourceDirectory { get; set; } = string.Empty;
    public string IncompleteDirectory { get; set; } = string.Empty;
    public string MoviesDestination { get; set; } = string.Empty;
    public string TvShowsDestination { get; set; } = string.Empty;
    public bool HasTmdbApiKey { get; set; }
    public bool HasTvdbApiKey { get; set; }
}
