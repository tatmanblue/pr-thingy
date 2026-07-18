using System.Text.Json;
using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.Infrastructure.Storage;

public sealed class FileBriefingRepository(string briefingsDirectory) : IBriefingRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public async Task<Briefing?> GetAsync(string repositoryStorageKey, int pullRequestNumber, CancellationToken cancellationToken)
    {
        string path = BriefingFilePath(repositoryStorageKey, pullRequestNumber);
        if (!File.Exists(path))
            return null;

        string json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<Briefing>(json, JsonOptions);
    }

    public async Task SaveAsync(Briefing briefing, CancellationToken cancellationToken)
    {
        string path = BriefingFilePath(briefing.RepositoryStorageKey, briefing.PullRequestNumber);
        string json = JsonSerializer.Serialize(briefing, JsonOptions);

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await AtomicFileWriter.WriteAsync(path, json, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<Briefing>> GetAllAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(briefingsDirectory))
            return [];

        List<Briefing> briefings = new List<Briefing>();
        foreach (string repositoryDirectory in Directory.EnumerateDirectories(briefingsDirectory))
        {
            foreach (string file in Directory.EnumerateFiles(repositoryDirectory, "pr-*.json"))
            {
                string json = await File.ReadAllTextAsync(file, cancellationToken);
                Briefing? briefing = JsonSerializer.Deserialize<Briefing>(json, JsonOptions);
                if (briefing is not null)
                    briefings.Add(briefing);
            }
        }

        return briefings;
    }

    public async Task<IReadOnlyList<Briefing>> GetAllForRepositoryAsync(string repositoryStorageKey, CancellationToken cancellationToken)
    {
        string repositoryDirectory = Path.Combine(briefingsDirectory, repositoryStorageKey);
        if (!Directory.Exists(repositoryDirectory))
            return [];

        List<Briefing> briefings = new List<Briefing>();
        foreach (string file in Directory.EnumerateFiles(repositoryDirectory, "pr-*.json"))
        {
            string json = await File.ReadAllTextAsync(file, cancellationToken);
            Briefing? briefing = JsonSerializer.Deserialize<Briefing>(json, JsonOptions);
            if (briefing is not null)
                briefings.Add(briefing);
        }

        return briefings;
    }

    public async Task SetReadStateAsync(string repositoryStorageKey, int pullRequestNumber, bool isRead, CancellationToken cancellationToken)
    {
        Briefing? existing = await GetAsync(repositoryStorageKey, pullRequestNumber, cancellationToken);
        if (existing is null)
            return;

        existing.IsRead = isRead;
        await SaveAsync(existing, cancellationToken);
    }

    public Task RemoveAsync(string repositoryStorageKey, int pullRequestNumber, CancellationToken cancellationToken)
    {
        string path = BriefingFilePath(repositoryStorageKey, pullRequestNumber);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    private string BriefingFilePath(string repositoryStorageKey, int pullRequestNumber) =>
        Path.Combine(briefingsDirectory, repositoryStorageKey, $"pr-{pullRequestNumber}.json");
}
