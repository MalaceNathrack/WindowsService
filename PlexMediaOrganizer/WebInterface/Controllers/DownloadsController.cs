using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PlexMediaOrganizer.Configuration;
using PlexMediaOrganizer.Services;
using PlexMediaOrganizer.WebInterface.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PlexMediaOrganizer.WebInterface.Controllers
{
    public class DownloadsController : Controller
    {
        private readonly PlexMediaOrganizerSettings _settings;
        private readonly IMediaProcessor _mediaProcessor;
        private readonly ILogger<DownloadsController> _logger;

        // Common video file extensions
        private static readonly string[] VideoExtensions = { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".m4v", ".mpg", ".mpeg", ".flv" };

        public DownloadsController(
            IOptions<PlexMediaOrganizerSettings> settings,
            IMediaProcessor mediaProcessor,
            ILogger<DownloadsController> logger)
        {
            _settings = settings.Value;
            _mediaProcessor = mediaProcessor;
            _logger = logger;
        }

        public IActionResult Index(string path = "")
        {
            try
            {
                var rootPath = _settings.SourceDirectory;
                var currentPath = rootPath;

                // If a subpath is specified, validate it's within the root path
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        // Normalize the path to handle different path formats
                        path = path.Replace('/', '\\');

                        // If the path starts with the drive letter or root folder name, make sure it's the correct one
                        if (Path.IsPathRooted(path))
                        {
                            path = Path.GetRelativePath(rootPath, path);
                        }

                        // Remove any "downloads\" prefix if present (case insensitive)
                        var rootFolderName = Path.GetFileName(rootPath);
                        if (path.StartsWith(rootFolderName + "\\", StringComparison.OrdinalIgnoreCase))
                        {
                            path = path.Substring(rootFolderName.Length + 1);
                        }

                        var fullPath = Path.GetFullPath(Path.Combine(rootPath, path));

                        if (fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullPath))
                        {
                            currentPath = fullPath;
                        }
                        else
                        {
                            _logger.LogWarning("Invalid path requested: {Path}", path);
                            return BadRequest("Invalid path");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing path: {Path}", path);
                        return BadRequest("Invalid path");
                    }
                }

                // Skip the incomplete directory
                var incompletePath = _settings.IncompleteDirectory;

                var model = new DownloadFolderViewModel
                {
                    CurrentPath = currentPath,
                    Files = new List<DownloadFileItem>()
                };

                // Get directories first
                var directories = Directory.GetDirectories(currentPath)
                    .Where(d => !d.Equals(incompletePath, StringComparison.OrdinalIgnoreCase))
                    .Select(d => new DownloadFileItem
                    {
                        FullPath = d,
                        Name = Path.GetFileName(d),
                        IsDirectory = true,
                        LastModified = Directory.GetLastWriteTime(d)
                    })
                    .OrderBy(d => d.Name)
                    .ToList();

                model.Files.AddRange(directories);

                // Get files
                var files = Directory.GetFiles(currentPath)
                    .Select(f => CreateFileItem(f))
                    .OrderBy(f => f.Name)
                    .ToList();

                model.Files.AddRange(files);

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving download folder contents");
                return View("Error", ex.Message);
            }
        }

        [HttpPost]
        [Route("api/downloads/approve")]
        public async Task<IActionResult> ApproveFile([FromBody] FileApprovalRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(request.FilePath) || !System.IO.File.Exists(request.FilePath))
                {
                    return BadRequest(new FileApprovalResponse
                    {
                        Success = false,
                        Message = "File not found"
                    });
                }

                if (string.IsNullOrEmpty(request.Title))
                {
                    return BadRequest(new FileApprovalResponse
                    {
                        Success = false,
                        Message = "Title is required"
                    });
                }

                if (string.IsNullOrEmpty(request.MediaType) ||
                    (request.MediaType != "Movie" && request.MediaType != "TvShow"))
                {
                    return BadRequest(new FileApprovalResponse
                    {
                        Success = false,
                        Message = "Valid media type (Movie or TvShow) is required"
                    });
                }

                string destinationPath;

                if (request.MediaType == "Movie")
                {
                    destinationPath = await _mediaProcessor.ProcessApprovedMovieAsync(
                        request.FilePath,
                        request.Title,
                        request.Year,
                        cancellationToken);
                }
                else // TvShow
                {
                    if (!request.Season.HasValue || !request.Episode.HasValue)
                    {
                        return BadRequest(new FileApprovalResponse
                        {
                            Success = false,
                            Message = "Season and episode numbers are required for TV shows"
                        });
                    }

                    destinationPath = await _mediaProcessor.ProcessApprovedTvEpisodeAsync(
                        request.FilePath,
                        request.Title,
                        request.Year,
                        request.Season.Value,
                        request.Episode.Value,
                        cancellationToken);
                }

                return Ok(new FileApprovalResponse
                {
                    Success = true,
                    Message = "File processed successfully",
                    DestinationPath = destinationPath
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing approved file");
                return StatusCode(500, new FileApprovalResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                });
            }
        }

        private DownloadFileItem CreateFileItem(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            var extension = fileInfo.Extension.ToLowerInvariant();
            var isMediaFile = VideoExtensions.Contains(extension);

            var fileItem = new DownloadFileItem
            {
                FullPath = filePath,
                Name = fileInfo.Name,
                Extension = extension,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                IsDirectory = false,
                IsMediaFile = isMediaFile
            };

            if (isMediaFile)
            {
                // Try to parse media info from filename
                ParseMediaInfo(fileItem);
            }

            return fileItem;
        }

        private void ParseMediaInfo(DownloadFileItem fileItem)
        {
            var filename = Path.GetFileNameWithoutExtension(fileItem.Name);

            // Try to detect if it's a TV show (S01E01 pattern)
            var tvShowRegex = new Regex(@"(.*?)[\.\s][Ss](\d{1,2})[Ee](\d{1,2})");
            var tvShowMatch = tvShowRegex.Match(filename);

            if (tvShowMatch.Success)
            {
                fileItem.MediaType = "TvShow";
                fileItem.SuggestedTitle = CleanTitle(tvShowMatch.Groups[1].Value);
                fileItem.SuggestedSeason = int.Parse(tvShowMatch.Groups[2].Value);
                fileItem.SuggestedEpisode = int.Parse(tvShowMatch.Groups[3].Value);
                return;
            }

            // Try to detect if it's a movie (Title.Year pattern)
            var movieRegex = new Regex(@"(.*?)[\.\s](\d{4})");
            var movieMatch = movieRegex.Match(filename);

            if (movieMatch.Success)
            {
                fileItem.MediaType = "Movie";
                fileItem.SuggestedTitle = CleanTitle(movieMatch.Groups[1].Value);
                fileItem.SuggestedYear = int.Parse(movieMatch.Groups[2].Value);
                return;
            }

            // If no pattern matched, assume it's a movie without year
            fileItem.MediaType = "Movie";
            fileItem.SuggestedTitle = CleanTitle(filename);
        }

        private string CleanTitle(string title)
        {
            // Replace dots and underscores with spaces
            title = title.Replace('.', ' ').Replace('_', ' ');

            // Remove common tags like 1080p, 720p, BluRay, etc.
            var commonTags = new[] {
                "1080p", "720p", "480p", "2160p", "bluray", "brrip", "dvdrip", "webdl", "webrip",
                "x264", "x265", "h264", "h265", "xvid", "aac", "ac3", "mp3", "hdtv", "proper", "repack"
            };

            foreach (var tag in commonTags)
            {
                title = Regex.Replace(title, $@"\b{tag}\b", "", RegexOptions.IgnoreCase);
            }

            // Clean up multiple spaces
            title = Regex.Replace(title, @"\s+", " ").Trim();

            // Capitalize first letter of each word
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(title.ToLower());
        }
    }
}
