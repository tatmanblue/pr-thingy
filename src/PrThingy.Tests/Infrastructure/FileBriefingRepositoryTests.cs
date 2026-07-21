using PrThingy.Core.Models;
using PrThingy.Infrastructure.Storage;
using PrThingy.Tests.TestHelpers;
using Xunit;

namespace PrThingy.Tests.Infrastructure;

public class FileBriefingRepositoryTests : IDisposable
{
    private readonly TempDirectoryFixture tempDirectory = new();
    private readonly FileBriefingRepository repository;

    public FileBriefingRepositoryTests()
    {
        repository = new FileBriefingRepository(tempDirectory.Path);
    }

    public void Dispose() => tempDirectory.Dispose();

    private static Briefing SampleBriefing(string repositoryStorageKey = "repo-abc12345", int number = 1) => new()
    {
        RepositoryStorageKey = repositoryStorageKey,
        RepositoryDisplayName = "repo",
        PullRequestNumber = number,
        Title = "Add feature",
        Author = "octocat",
        Body = "description",
        PullRequestUrl = "https://example.com/pr/1",
        UpdatedAtUtc = DateTimeOffset.UtcNow,
        Why = "adds a feature",
        HighImpactFiles = ["a.cs"],
        TopRisks = [new RiskItem { FilePath = "a.cs", Line = 10, Description = "risk" }],
        GeneratedAtUtc = DateTimeOffset.UtcNow,
        GeneratedByAgent = AgentType.Claude,
        IsWellFormed = true
    };

    [Fact]
    public async Task SaveAndGet_RoundTripsBriefing()
    {
        Briefing briefing = SampleBriefing();

        await repository.SaveAsync(briefing, CancellationToken.None);
        Briefing? loaded = await repository.GetAsync(briefing.RepositoryStorageKey, briefing.PullRequestNumber, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(briefing.Why, loaded.Why);
        Assert.Equal(briefing.HighImpactFiles, loaded.HighImpactFiles);
        Assert.Equal(briefing.TopRisks, loaded.TopRisks);
    }

    [Fact]
    public async Task GetAsync_NonExistentBriefing_ReturnsNull()
    {
        Briefing? loaded = await repository.GetAsync("no-such-repo", 999, CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task GetAllAsync_NoBriefingsSaved_ReturnsEmpty()
    {
        IReadOnlyList<Briefing> all = await repository.GetAllAsync(CancellationToken.None);

        Assert.Empty(all);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsBriefingsAcrossRepositories()
    {
        await repository.SaveAsync(SampleBriefing("repo-a-11111111", 1), CancellationToken.None);
        await repository.SaveAsync(SampleBriefing("repo-b-22222222", 2), CancellationToken.None);

        IReadOnlyList<Briefing> all = await repository.GetAllAsync(CancellationToken.None);

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task SetReadStateAsync_UpdatesPersistedReadFlag()
    {
        Briefing briefing = SampleBriefing();
        await repository.SaveAsync(briefing, CancellationToken.None);

        await repository.SetReadStateAsync(briefing.RepositoryStorageKey, briefing.PullRequestNumber, true, CancellationToken.None);
        Briefing? loaded = await repository.GetAsync(briefing.RepositoryStorageKey, briefing.PullRequestNumber, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.True(loaded.IsRead);
    }

    [Fact]
    public async Task SetReadStateAsync_NonExistentBriefing_DoesNothing()
    {
        await repository.SetReadStateAsync("no-such-repo", 999, true, CancellationToken.None);

        IReadOnlyList<Briefing> all = await repository.GetAllAsync(CancellationToken.None);
        Assert.Empty(all);
    }

    [Fact]
    public async Task GetAllForRepositoryAsync_OnlyReturnsBriefingsForThatRepository()
    {
        await repository.SaveAsync(SampleBriefing("repo-a-11111111", 1), CancellationToken.None);
        await repository.SaveAsync(SampleBriefing("repo-b-22222222", 2), CancellationToken.None);

        IReadOnlyList<Briefing> forRepoA = await repository.GetAllForRepositoryAsync("repo-a-11111111", CancellationToken.None);

        Briefing briefing = Assert.Single(forRepoA);
        Assert.Equal(1, briefing.PullRequestNumber);
    }

    [Fact]
    public async Task GetAllForRepositoryAsync_UnknownRepository_ReturnsEmpty()
    {
        IReadOnlyList<Briefing> forRepo = await repository.GetAllForRepositoryAsync("no-such-repo", CancellationToken.None);

        Assert.Empty(forRepo);
    }

    [Fact]
    public async Task RemoveAsync_DeletesBriefingSoItNoLongerLoads()
    {
        Briefing briefing = SampleBriefing();
        await repository.SaveAsync(briefing, CancellationToken.None);

        await repository.RemoveAsync(briefing.RepositoryStorageKey, briefing.PullRequestNumber, CancellationToken.None);
        Briefing? loaded = await repository.GetAsync(briefing.RepositoryStorageKey, briefing.PullRequestNumber, CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task RemoveAsync_NonExistentBriefing_DoesNotThrow()
    {
        await repository.RemoveAsync("no-such-repo", 999, CancellationToken.None);
    }

    // Guards against a real regression: briefings persisted before RiskItem existed store
    // TopRisks as plain strings on disk, and must keep loading rather than crashing the app.
    [Fact]
    public async Task GetAsync_LegacyPlainStringTopRisks_LoadsWithoutThrowing()
    {
        const string legacyJson = """
            {
              "RepositoryStorageKey": "repo-abc12345",
              "RepositoryDisplayName": "repo",
              "PullRequestNumber": 1,
              "Title": "Add feature",
              "Author": "octocat",
              "PullRequestUrl": "https://example.com/pr/1",
              "Why": "adds a feature",
              "HighImpactFiles": ["a.cs"],
              "TopRisks": ["a legacy plain-string risk"],
              "GeneratedAtUtc": "2026-07-16T20:58:05.015447+00:00",
              "SourcePullRequestUpdatedAtUtc": "2026-07-16T20:56:05+00:00",
              "GeneratedByAgent": 0,
              "IsWellFormed": true,
              "IsRead": false
            }
            """;

        DirectoryInfo repositoryDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.Path, "repo-abc12345"));
        await File.WriteAllTextAsync(Path.Combine(repositoryDirectory.FullName, "pr-1.json"), legacyJson);

        Briefing? loaded = await repository.GetAsync("repo-abc12345", 1, CancellationToken.None);

        Assert.NotNull(loaded);
        RiskItem risk = Assert.Single(loaded.TopRisks);
        Assert.Equal("a legacy plain-string risk", risk.Description);
        Assert.Null(risk.FilePath);
        Assert.Null(risk.Line);
    }

    [Fact]
    public async Task SaveAndGet_RoundTripsRecordWithNoAssessmentYet()
    {
        Briefing briefing = new Briefing
        {
            RepositoryStorageKey = "repo-abc12345",
            RepositoryDisplayName = "repo",
            PullRequestNumber = 1,
            Title = "Add feature",
            Author = "octocat",
            Body = "description",
            PullRequestUrl = "https://example.com/pr/1",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await repository.SaveAsync(briefing, CancellationToken.None);
        Briefing? loaded = await repository.GetAsync(briefing.RepositoryStorageKey, briefing.PullRequestNumber, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Null(loaded.Why);
        Assert.Null(loaded.GeneratedAtUtc);
        Assert.Null(loaded.GeneratedByAgent);
        Assert.Null(loaded.IsWellFormed);
        Assert.Empty(loaded.HighImpactFiles);
        Assert.Empty(loaded.TopRisks);
    }

    // Guards against a real regression: briefings persisted before Body/UpdatedAtUtc existed (and
    // before SourcePullRequestUpdatedAtUtc was removed) must keep loading with safe defaults.
    [Fact]
    public async Task GetAsync_LegacyRecordMissingBodyAndUpdatedAt_LoadsWithoutThrowing()
    {
        const string legacyJson = """
            {
              "RepositoryStorageKey": "repo-abc12345",
              "RepositoryDisplayName": "repo",
              "PullRequestNumber": 1,
              "Title": "Add feature",
              "Author": "octocat",
              "PullRequestUrl": "https://example.com/pr/1",
              "Why": "adds a feature",
              "HighImpactFiles": ["a.cs"],
              "TopRisks": [],
              "GeneratedAtUtc": "2026-07-16T20:58:05.015447+00:00",
              "SourcePullRequestUpdatedAtUtc": "2026-07-16T20:56:05+00:00",
              "GeneratedByAgent": 0,
              "IsWellFormed": true,
              "IsRead": false
            }
            """;

        DirectoryInfo repositoryDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.Path, "repo-abc12345"));
        await File.WriteAllTextAsync(Path.Combine(repositoryDirectory.FullName, "pr-1.json"), legacyJson);

        Briefing? loaded = await repository.GetAsync("repo-abc12345", 1, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(string.Empty, loaded.Body);
        Assert.Equal(DateTimeOffset.MinValue, loaded.UpdatedAtUtc);
        Assert.Equal("adds a feature", loaded.Why);
    }
}
