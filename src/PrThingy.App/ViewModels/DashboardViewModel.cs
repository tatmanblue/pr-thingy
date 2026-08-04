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
    private const double REVIEW_PANEL_FONT_SIZE_MIN = 8;
    private const double REVIEW_PANEL_FONT_SIZE_MAX = 32;

    private readonly IBriefingRepository briefingRepository;
    private readonly IWatchedRepositoryStore repositoryStore;
    private readonly IAppSettingsStore settingsStore;
    private readonly PrSyncOrchestrator orchestrator;
    private readonly ISyncLogService syncLog;
    private readonly IClipboardService clipboardService;
    private bool syncNowInFlight;

    // Master set of all cards for the current load, independent of the current
    // ShowUnreadOnly / SearchText filter. Briefings is always a filtered, order-
    // preserving projection of this list — never rebuilt from the repository on
    // every filter change.
    private readonly List<BriefingCardViewModel> allCards = [];

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
    public partial string? SearchText { get; set; }

    [ObservableProperty]
    public partial double ReviewPanelFontSize { get; set; } = AppSettings.REVIEW_PANEL_FONT_SIZE_DEFAULT;

    [ObservableProperty]
    public partial bool IsSyncing { get; set; }

    [ObservableProperty]
    public partial string? SyncStatusMessage { get; set; }

    partial void OnShowUnreadOnlyChanged(bool value)
    {
        ApplyFilter();
        _ = PersistShowUnreadOnlyAsync(value);
    }

    partial void OnSearchTextChanged(string? value) => ApplyFilter();

    partial void OnReviewPanelFontSizeChanged(double value)
    {
        foreach (BriefingCardViewModel card in Briefings)
            card.ReviewPanelFontSize = value;

        _ = PersistReviewPanelFontSizeAsync(value);
    }

    public async Task InitializeFromSettingsAsync()
    {
        AppSettings settings = await settingsStore.LoadAsync(CancellationToken.None);
        ShowUnreadOnly = settings.ShowUnreadOnly;
        ReviewPanelFontSize = settings.ReviewPanelFontSize;
    }

    private async Task PersistShowUnreadOnlyAsync(bool value)
    {
        AppSettings settings = await settingsStore.LoadAsync(CancellationToken.None);
        settings.ShowUnreadOnly = value;
        await settingsStore.SaveAsync(settings, CancellationToken.None);
    }

    private async Task PersistReviewPanelFontSizeAsync(double value)
    {
        AppSettings settings = await settingsStore.LoadAsync(CancellationToken.None);
        settings.ReviewPanelFontSize = value;
        await settingsStore.SaveAsync(settings, CancellationToken.None);
    }

    [RelayCommand]
    private void IncrementReviewPanelFontSize() =>
        ReviewPanelFontSize = Math.Min(REVIEW_PANEL_FONT_SIZE_MAX, ReviewPanelFontSize + 1);

    [RelayCommand]
    private void DecrementReviewPanelFontSize() =>
        ReviewPanelFontSize = Math.Max(REVIEW_PANEL_FONT_SIZE_MIN, ReviewPanelFontSize - 1);

    [RelayCommand]
    private void ResetReviewPanelFontSize() =>
        ReviewPanelFontSize = AppSettings.REVIEW_PANEL_FONT_SIZE_DEFAULT;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IReadOnlyList<Briefing> all = await briefingRepository.GetAllAsync(CancellationToken.None);

        foreach (BriefingCardViewModel existingCard in allCards)
            existingCard.PropertyChanged -= OnBriefingCardPropertyChanged;
        allCards.Clear();

        foreach (Briefing briefing in all.OrderByDescending(b => b.GeneratedAtUtc ?? b.CreatedAtUtc))
        {
            BriefingCardViewModel card = new BriefingCardViewModel(
                briefing, briefingRepository, clipboardService, repositoryStore, settingsStore, orchestrator);
            card.ReviewPanelFontSize = ReviewPanelFontSize;
            card.PropertyChanged += OnBriefingCardPropertyChanged;
            allCards.Add(card);
        }

        ApplyFilter();
    }

    private bool MatchesFilter(BriefingCardViewModel card)
    {
        if (ShowUnreadOnly && card.IsRead)
            return false;

        string? search = SearchText?.Trim();
        if (!string.IsNullOrEmpty(search) &&
            card.Title.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        return true;
    }

    // Reconciles Briefings to match allCards.Where(MatchesFilter) in place, preserving object
    // identity and order so unaffected cards never flicker and SelectedBriefing stays valid
    // across filter changes when the selected card still matches.
    private void ApplyFilter()
    {
        for (int i = Briefings.Count - 1; i >= 0; i--)
        {
            if (!MatchesFilter(Briefings[i]))
                Briefings.RemoveAt(i);
        }

        int insertIndex = 0;
        foreach (BriefingCardViewModel card in allCards)
        {
            if (!MatchesFilter(card))
                continue;

            if (insertIndex < Briefings.Count && ReferenceEquals(Briefings[insertIndex], card))
            {
                insertIndex++;
                continue;
            }

            Briefings.Insert(insertIndex, card);
            insertIndex++;
        }

        if (SelectedBriefing is not null && !MatchesFilter(SelectedBriefing))
            SelectedBriefing = null;
    }

    // A card's IsRead flipping (via its own ToggleReadCommand) can change whether it belongs
    // in the current filtered view; re-run the same predicate used everywhere else instead of
    // special-casing it here.
    private void OnBriefingCardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BriefingCardViewModel.IsRead))
            ApplyFilter();
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
            AppSettings settings = await settingsStore.LoadAsync(CancellationToken.None);
            IReadOnlyList<WatchedRepository> repositories = await repositoryStore.GetAllAsync(CancellationToken.None);

            foreach (WatchedRepository? repository in repositories.Where(r => r.Enabled))
            {
                try
                {
                    await orchestrator.SyncRepositoryAsync(repository, settings, CancellationToken.None);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    syncLog.Log(SyncLogLevel.Error, $"{repository.DisplayName}: sync failed — {ex.Message}");
                }
            }
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
