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

    public AgentEffortLevel[] AvailableEffortLevels { get; } = Enum.GetValues<AgentEffortLevel>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsClaudeSelected))]
    public partial AgentType SelectedAgent { get; set; }

    public bool IsClaudeSelected => SelectedAgent == AgentType.Claude;

    [ObservableProperty]
    public partial int PollingIntervalMinutes { get; set; }

    [ObservableProperty]
    public partial int MaxPullRequestsPerRepository { get; set; }

    [ObservableProperty]
    public partial string? AgentModel { get; set; }

    [ObservableProperty]
    public partial AgentEffortLevel AgentEffort { get; set; }

    [ObservableProperty]
    public partial int MaxDiffLengthChars { get; set; }

    [ObservableProperty]
    public partial bool RunScanOnClose { get; set; } = true;

    [ObservableProperty]
    public partial bool SyncOnStartup { get; set; } = true;

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    partial void OnSelectedAgentChanged(AgentType value) => StatusMessage = null;
    partial void OnPollingIntervalMinutesChanged(int value) => StatusMessage = null;
    partial void OnMaxPullRequestsPerRepositoryChanged(int value) => StatusMessage = null;
    partial void OnAgentModelChanged(string? value) => StatusMessage = null;
    partial void OnAgentEffortChanged(AgentEffortLevel value) => StatusMessage = null;
    partial void OnMaxDiffLengthCharsChanged(int value) => StatusMessage = null;

    [RelayCommand]
    public async Task LoadAsync()
    {
        AppSettings settings = await settingsStore.LoadAsync(CancellationToken.None);
        SelectedAgent = settings.SelectedAgent;
        PollingIntervalMinutes = settings.PollingIntervalMinutes;
        MaxPullRequestsPerRepository = settings.MaxPullRequestsPerRepository;
        AgentModel = settings.AgentModel;
        AgentEffort = settings.AgentEffort;
        MaxDiffLengthChars = settings.MaxDiffLengthChars;
        RunScanOnClose = settings.RunScanOnSettingsClose;
        originalRunScanOnClose = settings.RunScanOnSettingsClose;
        SyncOnStartup = settings.SyncOnStartup;
        StatusMessage = null;

        Repositories.Clear();
        originalRepositoryIds.Clear();
        foreach (WatchedRepository repository in await repositoryStore.GetAllAsync(CancellationToken.None))
        {
            Repositories.Add(new WatchedRepositoryRowViewModel(repository));
            originalRepositoryIds.Add(repository.Id);
        }
    }

    [RelayCommand]
    private async Task AddRepositoryAsync()
    {
        string? folder = await folderPickerService.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(folder))
            return;

        string displayName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        WatchedRepository repository = WatchedRepository.Create(displayName, folder);

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
            AgentModel = AgentModel,
            AgentEffort = AgentEffort,
            MaxDiffLengthChars = MaxDiffLengthChars,
            RunScanOnSettingsClose = RunScanOnClose,
            SyncOnStartup = SyncOnStartup
        }, CancellationToken.None);

        HashSet<string> currentIds = Repositories.Select(r => r.Id).ToHashSet();

        foreach (string? removedId in originalRepositoryIds.Except(currentIds))
            await repositoryStore.RemoveAsync(removedId, CancellationToken.None);

        foreach (WatchedRepositoryRowViewModel row in Repositories)
        {
            if (originalRepositoryIds.Contains(row.Id))
                await repositoryStore.UpdateAsync(row.ToModel(), CancellationToken.None);
            else
                await repositoryStore.AddAsync(row.ToModel(), CancellationToken.None);
        }

        originalRepositoryIds.Clear();
        foreach (string? id in currentIds)
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
