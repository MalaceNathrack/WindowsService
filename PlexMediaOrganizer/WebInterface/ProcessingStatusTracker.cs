using PlexMediaOrganizer.WebInterface.Models;
using System.Collections.Concurrent;

namespace PlexMediaOrganizer.WebInterface;

public class ProcessingStatusTracker : IProcessingStatusTracker
{
    private readonly ConcurrentQueue<ProcessingStatus> _statusItems = new();
    private readonly int _maxItems = 1000;
    private int _totalProcessed = 0;
    private int _successCount = 0;
    private int _errorCount = 0;
    private int _moviesCount = 0;
    private int _tvShowsCount = 0;
    private DateTime _lastProcessedTime = DateTime.MinValue;

    public void AddProcessingStatus(ProcessingStatus status)
    {
        _statusItems.Enqueue(status);
        _totalProcessed++;
        _lastProcessedTime = status.Timestamp;

        if (status.Status == "Success")
        {
            _successCount++;
            
            if (status.MediaType == MediaType.Movie)
            {
                _moviesCount++;
            }
            else if (status.MediaType == MediaType.TvShow || status.MediaType == MediaType.TvEpisode)
            {
                _tvShowsCount++;
            }
        }
        else if (status.Status == "Error")
        {
            _errorCount++;
        }

        // Trim the queue if it gets too large
        while (_statusItems.Count > _maxItems)
        {
            _statusItems.TryDequeue(out _);
        }
    }

    public ProcessingStatusSummary GetStatusSummary()
    {
        return new ProcessingStatusSummary
        {
            TotalProcessed = _totalProcessed,
            SuccessCount = _successCount,
            ErrorCount = _errorCount,
            MoviesCount = _moviesCount,
            TvShowsCount = _tvShowsCount,
            LastProcessedTime = _lastProcessedTime,
            RecentItems = GetRecentItems(10)
        };
    }

    public List<ProcessingStatus> GetRecentItems(int count = 50)
    {
        return _statusItems
            .OrderByDescending(s => s.Timestamp)
            .Take(count)
            .ToList();
    }

    public List<ProcessingStatus> GetErrorItems(int count = 50)
    {
        return _statusItems
            .Where(s => s.Status == "Error")
            .OrderByDescending(s => s.Timestamp)
            .Take(count)
            .ToList();
    }
}
