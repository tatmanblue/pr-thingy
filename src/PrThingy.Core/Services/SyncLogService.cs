using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.Core.Services;

// In-memory, thread-safe: written from PrSyncOrchestrator (which may run on the background
// polling service's thread or the UI-triggered sync command's thread) and read/subscribed to
// from UI ViewModels.
public sealed class SyncLogService : ISyncLogService
{
    private const int MAX_ENTRIES = 500;

    private readonly object gate = new();
    private readonly LinkedList<SyncLogEntry> entries = new();
    private int activeSyncCount;

    public bool IsSyncing
    {
        get
        {
            lock (gate)
                return activeSyncCount > 0;
        }
    }

    public IReadOnlyList<SyncLogEntry> Entries
    {
        get
        {
            lock (gate)
                return entries.ToList();
        }
    }

    public event EventHandler<SyncLogEntry>? EntryAdded;

    public event EventHandler<bool>? SyncingChanged;

    // Reference-counted so a manual "Sync Now" overlapping the background poll doesn't let the
    // first one to finish flip IsSyncing back to false while the other is still running.
    public void SyncStarted()
    {
        bool becameActive;
        lock (gate)
        {
            activeSyncCount++;
            becameActive = activeSyncCount == 1;
        }

        if (becameActive)
            SyncingChanged?.Invoke(this, true);
    }

    public void SyncFinished()
    {
        bool becameIdle;
        lock (gate)
        {
            activeSyncCount = Math.Max(0, activeSyncCount - 1);
            becameIdle = activeSyncCount == 0;
        }

        if (becameIdle)
            SyncingChanged?.Invoke(this, false);
    }

    public void Log(SyncLogLevel level, string message)
    {
        SyncLogEntry entry = new SyncLogEntry(DateTimeOffset.UtcNow, level, message);

        lock (gate)
        {
            entries.AddLast(entry);
            while (entries.Count > MAX_ENTRIES)
                entries.RemoveFirst();
        }

        EntryAdded?.Invoke(this, entry);
    }

    public void Clear()
    {
        lock (gate)
            entries.Clear();
    }
}
