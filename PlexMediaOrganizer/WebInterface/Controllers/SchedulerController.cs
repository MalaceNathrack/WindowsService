using Microsoft.AspNetCore.Mvc;
using PlexMediaOrganizer.Services;

namespace PlexMediaOrganizer.WebInterface.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchedulerController : ControllerBase
{
    private readonly ISchedulerService _schedulerService;
    private readonly IMediaProcessor _mediaProcessor;
    private readonly ILogger<SchedulerController> _logger;

    public SchedulerController(
        ISchedulerService schedulerService,
        IMediaProcessor mediaProcessor,
        ILogger<SchedulerController> logger)
    {
        _schedulerService = schedulerService;
        _mediaProcessor = mediaProcessor;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<List<ScheduledJobInfo>> GetScheduledJobs()
    {
        try
        {
            return _schedulerService.GetScheduledJobs();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scheduled jobs");
            return StatusCode(500, "An error occurred while retrieving scheduled jobs");
        }
    }

    [HttpPost("schedule")]
    public IActionResult ScheduleJob([FromBody] ScheduleJobRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.JobName) || string.IsNullOrEmpty(request.CronExpression))
            {
                return BadRequest("Job name and cron expression are required");
            }

            if (request.JobType == "ProcessDirectory")
            {
                _schedulerService.ScheduleJob(
                    request.JobName,
                    async () => await _mediaProcessor.ProcessDirectoryAsync(request.DirectoryPath ?? "C:\\downloads", CancellationToken.None),
                    request.CronExpression);
            }
            else
            {
                return BadRequest("Invalid job type");
            }

            return Ok(new { message = $"Job {request.JobName} scheduled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling job");
            return StatusCode(500, "An error occurred while scheduling the job");
        }
    }

    [HttpDelete("{jobName}")]
    public IActionResult CancelJob(string jobName)
    {
        try
        {
            _schedulerService.CancelJob(jobName);
            return Ok(new { message = $"Job {jobName} cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling job {JobName}", jobName);
            return StatusCode(500, "An error occurred while cancelling the job");
        }
    }

    public class ScheduleJobRequest
    {
        public string JobName { get; set; } = string.Empty;
        public string JobType { get; set; } = string.Empty;
        public string CronExpression { get; set; } = string.Empty;
        public string? DirectoryPath { get; set; }
    }
}
