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
}
