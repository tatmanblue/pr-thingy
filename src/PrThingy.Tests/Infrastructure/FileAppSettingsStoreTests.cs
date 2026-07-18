using PrThingy.Core.Models;
using PrThingy.Infrastructure.Storage;
using PrThingy.Tests.TestHelpers;
using Xunit;

namespace PrThingy.Tests.Infrastructure;

public class FileAppSettingsStoreTests : IDisposable
{
    private readonly TempDirectoryFixture tempDirectory = new();
    private readonly FileAppSettingsStore store;

    public FileAppSettingsStoreTests()
    {
        store = new FileAppSettingsStore(Path.Combine(tempDirectory.Path, "settings.json"));
    }

    public void Dispose() => tempDirectory.Dispose();

    [Fact]
    public async Task LoadAsync_NoFileYet_ReturnsDefaults()
    {
        AppSettings settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(AgentType.Claude, settings.SelectedAgent);
        Assert.Equal(60, settings.PollingIntervalMinutes);
        Assert.True(settings.StartMinimizedToTray);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsSettings()
    {
        AppSettings settings = new AppSettings
        {
            SelectedAgent = AgentType.Gemini,
            PollingIntervalMinutes = 15,
            StartMinimizedToTray = false
        };

        await store.SaveAsync(settings, CancellationToken.None);
        AppSettings loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(AgentType.Gemini, loaded.SelectedAgent);
        Assert.Equal(15, loaded.PollingIntervalMinutes);
        Assert.False(loaded.StartMinimizedToTray);
    }

    [Fact]
    public async Task SaveAsync_Twice_OverwritesPreviousValue()
    {
        await store.SaveAsync(new AppSettings { PollingIntervalMinutes = 10 }, CancellationToken.None);
        await store.SaveAsync(new AppSettings { PollingIntervalMinutes = 20 }, CancellationToken.None);

        AppSettings loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(20, loaded.PollingIntervalMinutes);
    }
}
