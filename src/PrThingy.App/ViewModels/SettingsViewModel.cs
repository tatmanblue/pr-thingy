using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrThingy.App.Services;
using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IWatchedRepositoryStore repositoryStore;
    private readonly IAppSettingsStore settingsStore;
    private readonly IFolderPickerService folderPickerService;
    private readonly DashboardViewModel dashboard;

    // Snapshot of what's actually persisted, taken at LoadAsync. Used to diff against
    // Repositories at Save time, and to decide Cancel's post-close scan without honoring
    // any unsaved edits (including the RunScanOnClose checkbox itself).
    private readonly HashSet<string> originalRepositoryIds = [];
    private bool originalRunScanOnClose;

    public SettingsViewModel(
        IWatchedRepositoryStore repositoryStore,
        IAppSettingsStore settingsStore,
        IFolderPickerService folderPickerService,
        DashboardViewModel dashboard)
    {
        this.repositoryStore = repositoryStore;
        this.settingsStore = settingsStore;
        this.folderPickerService = folderPickerService;
        this.dashboard = dashboard;
    }

    public event Action? CloseRequested;

    public ObservableCollection<WatchedRepositoryRowViewModel> Repositories { get; } = [];

    public AgentType[] AvailableAgents { get; } = Enum.GetValues<AgentType>();

    [ObservableProperty]
    public partial AgentType SelectedAgent { get; set; }

    [ObservableProperty]
    public partial int PollingIntervalMinutes { get; set; }

    [ObservableProperty]
    public partial int MaxPullRequestsPerRepository { get; set; }

    [ObservableProperty]
    public partial bool RunScanOnClose { get; set; } = true;

    [ObservableProperty]
    public partial bool SyncOnStartup { get; set; } = true;

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    partial void OnSelectedAgentChanged(AgentType value) => StatusMessage = null;
    partial void OnPollingIntervalMinutesChanged(int value) => StatusMessage = null;
    partial void OnMaxPullRequestsPerRepositoryChanged(int value) => StatusMessage = null;

    [RelayCommand]
    public async Task LoadAsync()
    {
        var settings = await settingsStore.LoadAsync(CancellationToken.None);
        SelectedAgent = settings.SelectedAgent;
        PollingIntervalMinutes = settings.PollingIntervalMinutes;
        MaxPullRequestsPerRepository = settings.MaxPullRequestsPerRepository;
        RunScanOnClose = settings.RunScanOnSettingsClose;
        originalRunScanOnClose = settings.RunScanOnSettingsClose;
        SyncOnStartup = settings.SyncOnStartup;
        StatusMessage = null;

        Repositories.Clear();
        originalRepositoryIds.Clear();
        foreach (var repository in await repositoryStore.GetAllAsync(CancellationToken.None))
        {
            Repositories.Add(new WatchedRepositoryRowViewModel(repository));
            originalRepositoryIds.Add(repository.Id);
        }
    }

    [RelayCommand]
    private async Task AddRepositoryAsync()
    {
        var folder = await folderPickerService.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(folder))
            return;

        var displayName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var repository = WatchedRepository.Create(displayName, folder);

        // Staged only — not persisted until Save/SaveAndClose, so Cancel can discard it.
        Repositories.Add(new WatchedRepositoryRowViewModel(repository));
        StatusMessage = null;
    }

    [RelayCommand]
    private void RemoveRepository(WatchedRepositoryRowViewModel? row)
    {
        if (row is null)
            return;

        // Staged only — the row is only actually deleted from disk if this removal survives to Save.
        Repositories.Remove(row);
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await settingsStore.SaveAsync(new AppSettings
        {
            SelectedAgent = SelectedAgent,
            PollingIntervalMinutes = PollingIntervalMinutes,
            MaxPullRequestsPerRepository = MaxPullRequestsPerRepository,
            RunScanOnSettingsClose = RunScanOnClose,
            SyncOnStartup = SyncOnStartup
        }, CancellationToken.None);

        var currentIds = Repositories.Select(r => r.Id).ToHashSet();

        foreach (var removedId in originalRepositoryIds.Except(currentIds))
            await repositoryStore.RemoveAsync(removedId, CancellationToken.None);

        foreach (var row in Repositories)
        {
            if (originalRepositoryIds.Contains(row.Id))
                await repositoryStore.UpdateAsync(row.ToModel(), CancellationToken.None);
            else
                await repositoryStore.AddAsync(row.ToModel(), CancellationToken.None);
        }

        originalRepositoryIds.Clear();
        foreach (var id in currentIds)
            originalRepositoryIds.Add(id);
        originalRunScanOnClose = RunScanOnClose;

        StatusMessage = "Saved";
    }

    [RelayCommand]
    private async Task SaveAndCloseAsync()
    {
        await SaveAsync();
        if (RunScanOnClose)
            _ = dashboard.SyncNowCommand.ExecuteAsync(null);

        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        // Discard every staged edit, including the checkbox itself — the scan decision uses
        // whatever was actually persisted last, not whatever the user was mid-editing.
        if (originalRunScanOnClose)
            _ = dashboard.SyncNowCommand.ExecuteAsync(null);

        CloseRequested?.Invoke();
    }
}
