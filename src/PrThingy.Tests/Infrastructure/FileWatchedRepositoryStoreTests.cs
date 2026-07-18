using PrThingy.Core.Models;
using PrThingy.Infrastructure.Storage;
using PrThingy.Tests.TestHelpers;
using Xunit;

namespace PrThingy.Tests.Infrastructure;

public class FileWatchedRepositoryStoreTests : IDisposable
{
    private readonly TempDirectoryFixture tempDirectory = new();
    private readonly FileWatchedRepositoryStore store;

    public FileWatchedRepositoryStoreTests()
    {
        store = new FileWatchedRepositoryStore(Path.Combine(tempDirectory.Path, "repositories.json"));
    }

    public void Dispose() => tempDirectory.Dispose();

    [Fact]
    public async Task GetAllAsync_NoFileYet_ReturnsEmpty()
    {
        var all = await store.GetAllAsync(CancellationToken.None);

        Assert.Empty(all);
    }

    [Fact]
    public async Task AddAsync_ThenGetAllAsync_RoundTrips()
    {
        var repository = WatchedRepository.Create("my-repo", "/tmp/my-repo");

        await store.AddAsync(repository, CancellationToken.None);
        var all = await store.GetAllAsync(CancellationToken.None);

        Assert.Single(all);
        Assert.Equal(repository.Id, all[0].Id);
        Assert.Equal(repository.LocalPath, all[0].LocalPath);
    }

    [Fact]
    public async Task UpdateAsync_ExistingRepository_PersistsChanges()
    {
        var repository = WatchedRepository.Create("my-repo", "/tmp/my-repo");
        await store.AddAsync(repository, CancellationToken.None);

        repository.DisplayName = "renamed-repo";
        repository.Enabled = false;
        await store.UpdateAsync(repository, CancellationToken.None);

        var all = await store.GetAllAsync(CancellationToken.None);
        Assert.Equal("renamed-repo", all[0].DisplayName);
        Assert.False(all[0].Enabled);
    }

    [Fact]
    public async Task UpdateAsync_UnknownRepository_Throws()
    {
        var repository = WatchedRepository.Create("ghost-repo", "/tmp/ghost");

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdateAsync(repository, CancellationToken.None));
    }

    [Fact]
    public async Task RemoveAsync_ExistingRepository_RemovesIt()
    {
        var repository = WatchedRepository.Create("my-repo", "/tmp/my-repo");
        await store.AddAsync(repository, CancellationToken.None);

        await store.RemoveAsync(repository.Id, CancellationToken.None);

        var all = await store.GetAllAsync(CancellationToken.None);
        Assert.Empty(all);
    }

    [Fact]
    public async Task RemoveAsync_UnknownId_IsNoOp()
    {
        await store.RemoveAsync("does-not-exist", CancellationToken.None);

        var all = await store.GetAllAsync(CancellationToken.None);
        Assert.Empty(all);
    }
}
