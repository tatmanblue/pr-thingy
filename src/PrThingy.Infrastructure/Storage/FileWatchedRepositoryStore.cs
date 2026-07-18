using System.Text.Json;
using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.Infrastructure.Storage;

public sealed class FileWatchedRepositoryStore(string repositoriesFilePath) : IWatchedRepositoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public async Task<IReadOnlyList<WatchedRepository>> GetAllAsync(CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            return await ReadAllUnlockedAsync(cancellationToken);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task AddAsync(WatchedRepository repository, CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            List<WatchedRepository> all = (await ReadAllUnlockedAsync(cancellationToken)).ToList();
            all.Add(repository);
            await WriteAllUnlockedAsync(all, cancellationToken);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task UpdateAsync(WatchedRepository repository, CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            List<WatchedRepository> all = (await ReadAllUnlockedAsync(cancellationToken)).ToList();
            int index = all.FindIndex(r => r.Id == repository.Id);
            if (index < 0)
                throw new InvalidOperationException($"Watched repository '{repository.Id}' not found.");

            all[index] = repository;
            await WriteAllUnlockedAsync(all, cancellationToken);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task RemoveAsync(string repositoryId, CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            List<WatchedRepository> all = (await ReadAllUnlockedAsync(cancellationToken)).ToList();
            all.RemoveAll(r => r.Id == repositoryId);
            await WriteAllUnlockedAsync(all, cancellationToken);
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<List<WatchedRepository>> ReadAllUnlockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(repositoriesFilePath))
            return [];

        string json = await File.ReadAllTextAsync(repositoriesFilePath, cancellationToken);
        return JsonSerializer.Deserialize<List<WatchedRepository>>(json, JsonOptions) ?? [];
    }

    private Task WriteAllUnlockedAsync(List<WatchedRepository> repositories, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(repositories, JsonOptions);
        return AtomicFileWriter.WriteAsync(repositoriesFilePath, json, cancellationToken);
    }
}
