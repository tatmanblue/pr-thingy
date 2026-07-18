using PrThingy.Core.Models;

namespace PrThingy.Core.Abstractions;

public interface ISyncLogService
{
    bool IsSyncing { get; }

    IReadOnlyList<SyncLogEntry> Entries { get; }

    event EventHandler<SyncLogEntry>? EntryAdded;

    event EventHandler<bool>? SyncingChanged;

    void SyncStarted();

    void SyncFinished();

    void Log(SyncLogLevel level, string message);

    void Clear();
}
