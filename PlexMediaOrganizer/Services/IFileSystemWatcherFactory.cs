using System.IO;

namespace PlexMediaOrganizer.Services;

public interface IFileSystemWatcherFactory
{
    FileSystemWatcher CreateWatcher(string path, string filter = "*.*");
}
