namespace PlexMediaOrganizer.Services;

public interface ISchedulerService
{
    void ScheduleJob(string jobName, Action action, string cronExpression);
    void ScheduleJob(string jobName, Func<Task> action, string cronExpression);
    void CancelJob(string jobName);
    List<ScheduledJobInfo> GetScheduledJobs();
}

public class ScheduledJobInfo
{
    public string JobName { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public DateTime? NextRunTime { get; set; }
    public DateTime? LastRunTime { get; set; }
    public bool IsRunning { get; set; }
}
