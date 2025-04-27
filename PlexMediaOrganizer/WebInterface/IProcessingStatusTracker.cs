using PlexMediaOrganizer.WebInterface.Models;

namespace PlexMediaOrganizer.WebInterface;

public interface IProcessingStatusTracker
{
    void AddProcessingStatus(ProcessingStatus status);
    ProcessingStatusSummary GetStatusSummary();
    List<ProcessingStatus> GetRecentItems(int count = 50);
    List<ProcessingStatus> GetErrorItems(int count = 50);
}
