using System;
using System.Collections.Generic;

namespace PlexMediaOrganizer.WebInterface.Models
{
    public class DownloadFolderViewModel
    {
        public List<DownloadFileItem> Files { get; set; } = new List<DownloadFileItem>();
        public string CurrentPath { get; set; } = string.Empty;
    }

    public class DownloadFileItem
    {
        public string FullPath { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsMediaFile { get; set; }
        public string MediaType { get; set; } = string.Empty;
        public string SuggestedTitle { get; set; } = string.Empty;
        public int? SuggestedYear { get; set; }
        public int? SuggestedSeason { get; set; }
        public int? SuggestedEpisode { get; set; }
    }

    public class FileApprovalRequest
    {
        public string FilePath { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int? Year { get; set; }
        public int? Season { get; set; }
        public int? Episode { get; set; }
        public string MediaType { get; set; } = string.Empty;
    }

    public class FileApprovalResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
    }
}
