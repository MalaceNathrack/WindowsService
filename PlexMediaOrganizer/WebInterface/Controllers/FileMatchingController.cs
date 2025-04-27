using Microsoft.AspNetCore.Mvc;
using PlexMediaOrganizer;
using PlexMediaOrganizer.Data.Repositories;
using PlexMediaOrganizer.Services;
using PlexMediaOrganizer.WebInterface.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

public class FileMatchingController : Controller
{
    private readonly IPendingFileRepository _pendingRepo;
    private readonly IMetadataService _metadataService;
    private readonly IMediaProcessor _mediaProcessor;
    private readonly ILogger<FileMatchingController> _logger;

    public FileMatchingController(
        IPendingFileRepository pendingRepo,
        IMetadataService metadataService,
        IMediaProcessor mediaProcessor,
        ILogger<FileMatchingController> logger)
    {
        _pendingRepo = pendingRepo;
        _metadataService = metadataService;
        _mediaProcessor = mediaProcessor;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var pendingFiles = await _pendingRepo.GetPendingFilesAsync();
        return View(pendingFiles);
    }

    [HttpPost]
    public async Task<IActionResult> SearchMetadata([FromBody] SearchRequest request)
    {
        var results = request.MediaType.ToLower() == "movie" 
            ? await _metadataService.GetMovieMetadataAsync(request.Query)
            : await _metadataService.GetTvShowMetadataAsync(request.Query);
        return Json(results);
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmMatch([FromBody] MatchConfirmation request)
    {
        var pendingFile = await _pendingRepo.GetByIdAsync(request.PendingFileId);
        if (pendingFile == null)
        {
            return NotFound();
        }

        // Update pending file with confirmed metadata
        pendingFile.Status = "Matched";
        pendingFile.SuggestedTitle = request.Title;
        pendingFile.SuggestedYear = request.Year;
        pendingFile.MediaType = request.MediaType;
        pendingFile.Season = request.Season;
        pendingFile.Episode = request.Episode;
        pendingFile.TmdbId = request.TmdbId;

        await _pendingRepo.UpdateAsync(pendingFile);

        // Process the file with confirmed metadata
        try 
        {
            string destinationPath;
            if (request.MediaType == "Movie")
            {
                destinationPath = await _mediaProcessor.ProcessApprovedMovieAsync(
                    pendingFile.FilePath,
                    request.Title,
                    request.Year,
                    CancellationToken.None);
            }
            else if (request.MediaType == "TvEpisode")
            {
                destinationPath = await _mediaProcessor.ProcessApprovedTvEpisodeAsync(
                    pendingFile.FilePath,
                    request.Title,
                    request.Year,
                    request.Season ?? 1,
                    request.Episode ?? 1,
                    CancellationToken.None);
            }
            else
            {
                return BadRequest($"Unsupported media type: {request.MediaType}");
            }

            return Ok(new { destinationPath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file {FilePath}", pendingFile.FilePath);
            return StatusCode(500, ex.Message);
        }
    }
}




















