using System.IO;

namespace PlexMediaOrganizer.Services;

public class FileSystemWatcherFactory : IFileSystemWatcherFactory
{
    public FileSystemWatcher CreateWatcher(string path, string filter = "*.*")
    {
        var watcher = new FileSystemWatcher(path, filter)
        {
            NotifyFilter = NotifyFilters.Attributes
                         | NotifyFilters.CreationTime
                         | NotifyFilters.DirectoryName
                         | NotifyFilters.FileName
                         | NotifyFilters.LastAccess
                         | NotifyFilters.LastWrite
                         | NotifyFilters.Security
                         | NotifyFilters.Size,
            IncludeSubdirectories = true,
            EnableRaisingEvents = false
        };

        return watcher;
    }
}
