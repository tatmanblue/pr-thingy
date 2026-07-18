using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrThingy.App.Services;
using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;
using PrThingy.Core.Services;

namespace PrThingy.App.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IBriefingRepository briefingRepository;
    private readonly IWatchedRepositoryStore repositoryStore;
    private readonly IAppSettingsStore settingsStore;
    private readonly PrSyncOrchestrator orchestrator;
    private readonly ISyncLogService syncLog;
    private readonly IClipboardService clipboardService;
    private bool syncNowInFlight;

    public DashboardViewModel(
        IBriefingRepository briefingRepository,
        IWatchedRepositoryStore repositoryStore,
        IAppSettingsStore settingsStore,
        PrSyncOrchestrator orchestrator,
        ISyncLogService syncLog,
        IClipboardService clipboardService)
    {
        this.briefingRepository = briefingRepository;
        this.repositoryStore = repositoryStore;
        this.settingsStore = settingsStore;
        this.orchestrator = orchestrator;
        this.syncLog = syncLog;
        this.clipboardService = clipboardService;

        IsSyncing = syncLog.IsSyncing;
        syncLog.SyncingChanged += OnSyncLogSyncingChanged;
        syncLog.EntryAdded += OnSyncLogEntryAdded;
    }

    public ObservableCollection<BriefingCardViewModel> Briefings { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedBriefing))]
    public partial BriefingCardViewModel? SelectedBriefing { get; set; }

    public bool HasSelectedBriefing => SelectedBriefing is not null;

    [ObservableProperty]
    public partial bool ShowUnreadOnly { get; set; }

    [ObservableProperty]
    public partial bool IsSyncing { get; set; }

    [ObservableProperty]
    public partial string? SyncStatusMessage { get; set; }

    partial void OnShowUnreadOnlyChanged(bool value) => _ = LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        var all = await briefingRepository.GetAllAsync(CancellationToken.None);

        foreach (var existingCard in Briefings)
            existingCard.PropertyChanged -= OnBriefingCardPropertyChanged;
        Briefings.Clear();

        foreach (var briefing in all.OrderByDescending(b => b.GeneratedAtUtc))
        {
            if (ShowUnreadOnly && briefing.IsRead)
                continue;

            var card = new BriefingCardViewModel(briefing, briefingRepository, clipboardService);
            card.PropertyChanged += OnBriefingCardPropertyChanged;
            Briefings.Add(card);
        }
    }

    // While "unread only" is active, a card marked read should disappear immediately rather
    // than waiting for the next full reload.
    private void OnBriefingCardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BriefingCardViewModel.IsRead))
            return;

        if (sender is not BriefingCardViewModel { IsRead: true } card || !ShowUnreadOnly)
            return;

        if (SelectedBriefing == card)
            SelectedBriefing = null;

        card.PropertyChanged -= OnBriefingCardPropertyChanged;
        Briefings.Remove(card);
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        if (syncNowInFlight)
            return;

        syncNowInFlight = true;
        syncLog.SyncStarted();
        try
        {
            var settings = await settingsStore.LoadAsync(CancellationToken.None);
            var repositories = await repositoryStore.GetAllAsync(CancellationToken.None);

            foreach (var repository in repositories.Where(r => r.Enabled))
                await orchestrator.SyncRepositoryAsync(repository, settings, CancellationToken.None);
        }
        finally
        {
            syncLog.SyncFinished();
            syncNowInFlight = false;
        }
    }

    // Fires from whichever sync triggered it — the manual command above or the background
    // polling service — so the dashboard reflects sync activity regardless of source.
    private void OnSyncLogSyncingChanged(object? sender, bool isSyncing)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsSyncing = isSyncing;
            if (isSyncing)
                return;

            SyncStatusMessage = null;
            _ = LoadAsync();
        });
    }

    private void OnSyncLogEntryAdded(object? sender, SyncLogEntry entry) =>
        Dispatcher.UIThread.Post(() => SyncStatusMessage = entry.Message);
}
