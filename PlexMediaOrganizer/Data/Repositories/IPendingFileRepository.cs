using PlexMediaOrganizer;

namespace PlexMediaOrganizer.Data.Repositories;

public interface IPendingFileRepository
{
    Task<IEnumerable<PendingFile>> GetPendingFilesAsync();
    Task<PendingFile?> GetByIdAsync(int id);
    Task<PendingFile> AddAsync(PendingFile pendingFile);
    Task<PendingFile> UpdateAsync(PendingFile pendingFile);
}
