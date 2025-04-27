using Cronos;
using System.Collections.Concurrent;

namespace PlexMediaOrganizer.Services;

public class SchedulerService : ISchedulerService, IDisposable
{
    private readonly ILogger<SchedulerService> _logger;
    private readonly ConcurrentDictionary<string, ScheduledJob> _jobs = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _jobCancellationTokens = new();
    private bool _disposed;

    public SchedulerService(ILogger<SchedulerService> logger)
    {
        _logger = logger;
    }

    public void ScheduleJob(string jobName, Action action, string cronExpression)
    {
        ScheduleJob(jobName, () => 
        {
            action();
            return Task.CompletedTask;
        }, cronExpression);
    }

    public void ScheduleJob(string jobName, Func<Task> action, string cronExpression)
    {
        if (string.IsNullOrEmpty(jobName))
            throw new ArgumentException("Job name cannot be empty", nameof(jobName));

        if (action == null)
            throw new ArgumentNullException(nameof(action));

        if (string.IsNullOrEmpty(cronExpression))
            throw new ArgumentException("Cron expression cannot be empty", nameof(cronExpression));

        // Cancel existing job with the same name if it exists
        CancelJob(jobName);

        try
        {
            var cronSchedule = CronExpression.Parse(cronExpression);
            var job = new ScheduledJob
            {
                JobName = jobName,
                CronExpression = cronExpression,
                Action = action,
                CronSchedule = cronSchedule
            };

            _jobs[jobName] = job;

            var cts = new CancellationTokenSource();
            _jobCancellationTokens[jobName] = cts;

            // Start the job
            _ = RunJobAsync(job, cts.Token);

            _logger.LogInformation("Scheduled job {JobName} with cron expression {CronExpression}", jobName, cronExpression);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling job {JobName}", jobName);
            throw;
        }
    }

    public void CancelJob(string jobName)
    {
        if (_jobCancellationTokens.TryRemove(jobName, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _logger.LogInformation("Cancelled job {JobName}", jobName);
        }

        _jobs.TryRemove(jobName, out _);
    }

    public List<ScheduledJobInfo> GetScheduledJobs()
    {
        return _jobs.Values.Select(j => new ScheduledJobInfo
        {
            JobName = j.JobName,
            CronExpression = j.CronExpression,
            NextRunTime = GetNextOccurrence(j.CronSchedule),
            LastRunTime = j.LastRunTime,
            IsRunning = j.IsRunning
        }).ToList();
    }

    private async Task RunJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                var nextOccurrence = job.CronSchedule.GetNextOccurrence(now);

                if (nextOccurrence.HasValue)
                {
                    var delay = nextOccurrence.Value - now;
                    _logger.LogDebug("Job {JobName} will run in {Delay}", job.JobName, delay);

                    await Task.Delay(delay, cancellationToken);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            _logger.LogInformation("Running job {JobName}", job.JobName);
                            job.IsRunning = true;
                            await job.Action();
                            job.LastRunTime = DateTime.UtcNow;
                            _logger.LogInformation("Job {JobName} completed successfully", job.JobName);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error executing job {JobName}", job.JobName);
                        }
                        finally
                        {
                            job.IsRunning = false;
                        }
                    }
                }
                else
                {
                    // No more occurrences, exit the loop
                    _logger.LogInformation("No more occurrences for job {JobName}", job.JobName);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Job was cancelled, this is expected
            _logger.LogInformation("Job {JobName} was cancelled", job.JobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in job scheduler for {JobName}", job.JobName);
        }
    }

    private DateTime? GetNextOccurrence(CronExpression cronExpression)
    {
        return cronExpression.GetNextOccurrence(DateTime.UtcNow);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            foreach (var cts in _jobCancellationTokens.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }

            _jobCancellationTokens.Clear();
            _jobs.Clear();
        }

        _disposed = true;
    }

    private class ScheduledJob
    {
        public string JobName { get; set; } = string.Empty;
        public string CronExpression { get; set; } = string.Empty;
        public Func<Task> Action { get; set; } = () => Task.CompletedTask;
        public CronExpression CronSchedule { get; set; } = null!;
        public DateTime? LastRunTime { get; set; }
        public bool IsRunning { get; set; }
    }
}
