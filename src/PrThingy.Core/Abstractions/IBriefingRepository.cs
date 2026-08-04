using PrThingy.Core.Models;

namespace PrThingy.Core.Abstractions;

public interface IBriefingRepository
{
    Task<Briefing?> GetAsync(string repositoryStorageKey, int pullRequestNumber, CancellationToken cancellationToken);

    Task SaveAsync(Briefing briefing, CancellationToken cancellationToken);

    Task<IReadOnlyList<Briefing>> GetAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Briefing>> GetAllForRepositoryAsync(string repositoryStorageKey, CancellationToken cancellationToken);

    Task SetReadStateAsync(string repositoryStorageKey, int pullRequestNumber, bool isRead, CancellationToken cancellationToken);

    Task RemoveAsync(string repositoryStorageKey, int pullRequestNumber, CancellationToken cancellationToken);

    Task DeleteAllForRepositoryAsync(string repositoryStorageKey, CancellationToken cancellationToken);

    // Deletes any on-disk repository briefing folder whose storage key isn't in activeStorageKeys —
    // e.g. left behind by a repo that was removed and re-added under a new StorageKey before
    // DeleteAllForRepositoryAsync existed, or if cleanup was interrupted.
    Task DeleteOrphanedRepositoriesAsync(IReadOnlyCollection<string> activeStorageKeys, CancellationToken cancellationToken);
}
