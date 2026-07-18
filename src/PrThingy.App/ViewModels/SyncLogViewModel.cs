using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.App.ViewModels;

public partial class SyncLogViewModel : ViewModelBase
{
    private readonly ISyncLogService syncLog;

    public SyncLogViewModel(ISyncLogService syncLog)
    {
        this.syncLog = syncLog;

        foreach (var entry in syncLog.Entries)
            Entries.Add(new SyncLogEntryViewModel(entry));

        syncLog.EntryAdded += OnEntryAdded;
    }

    public ObservableCollection<SyncLogEntryViewModel> Entries { get; } = [];

    private void OnEntryAdded(object? sender, SyncLogEntry entry) =>
        Dispatcher.UIThread.Post(() => Entries.Add(new SyncLogEntryViewModel(entry)));

    [RelayCommand]
    private void Clear()
    {
        Entries.Clear();
        syncLog.Clear();
    }
}
