using PrThingy.Core.Models;

namespace PrThingy.Core.Abstractions;

public interface IWatchedRepositoryStore
{
    Task<IReadOnlyList<WatchedRepository>> GetAllAsync(CancellationToken cancellationToken);

    Task AddAsync(WatchedRepository repository, CancellationToken cancellationToken);

    Task UpdateAsync(WatchedRepository repository, CancellationToken cancellationToken);

    Task RemoveAsync(string repositoryId, CancellationToken cancellationToken);
}
