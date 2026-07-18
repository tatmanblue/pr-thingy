using PrThingy.Core.Models;
using PrThingy.Core.Services;
using Xunit;

namespace PrThingy.Tests.Core;

public class SyncLogServiceTests
{
    [Fact]
    public void SyncStartedThenFinished_TogglesIsSyncing()
    {
        var service = new SyncLogService();
        var observedStates = new List<bool>();
        service.SyncingChanged += (_, isSyncing) => observedStates.Add(isSyncing);

        service.SyncStarted();
        Assert.True(service.IsSyncing);

        service.SyncFinished();
        Assert.False(service.IsSyncing);

        Assert.Equal([true, false], observedStates);
    }

    [Fact]
    public void OverlappingSyncs_StayActiveUntilAllFinish()
    {
        var service = new SyncLogService();

        service.SyncStarted();
        service.SyncStarted();
        service.SyncFinished();

        Assert.True(service.IsSyncing);

        service.SyncFinished();

        Assert.False(service.IsSyncing);
    }

    [Fact]
    public void Log_AddsEntryAndRaisesEvent()
    {
        var service = new SyncLogService();
        SyncLogEntry? raised = null;
        service.EntryAdded += (_, entry) => raised = entry;

        service.Log(SyncLogLevel.Warning, "something happened");

        Assert.NotNull(raised);
        Assert.Equal(SyncLogLevel.Warning, raised.Level);
        Assert.Equal("something happened", raised.Message);
        Assert.Single(service.Entries);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var service = new SyncLogService();
        service.Log(SyncLogLevel.Info, "one");
        service.Log(SyncLogLevel.Info, "two");

        service.Clear();

        Assert.Empty(service.Entries);
    }
}
