using System.Diagnostics;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrThingy.App.Services;
using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;
using PrThingy.Core.Services;

namespace PrThingy.App.ViewModels;

public partial class BriefingCardViewModel : ViewModelBase
{
    private const string COPY_LINK_LABEL = "Copy Link";
    private const string COPY_LINK_CONFIRMATION_LABEL = "Copied!";

    private static readonly TimeSpan NEW_THRESHOLD = TimeSpan.FromHours(24);
    private static readonly TimeSpan RECENT_THRESHOLD = TimeSpan.FromDays(14);
    private static readonly TimeSpan AGING_THRESHOLD = TimeSpan.FromDays(30);

    private static readonly IBrush AGE_NEW_BRUSH = new SolidColorBrush(Color.Parse("#3344CC88"));
    private static readonly IBrush AGE_RECENT_BRUSH = new SolidColorBrush(Color.Parse("#33888888"));
    private static readonly IBrush AGE_AGING_BRUSH = new SolidColorBrush(Color.Parse("#33FF9800"));
    private static readonly IBrush AGE_OLD_BRUSH = new SolidColorBrush(Color.Parse("#33FF4444"));

    private static readonly IBrush DRAFT_BRUSH = new SolidColorBrush(Color.Parse("#33888888"));
    private static readonly IBrush APPROVED_BRUSH = new SolidColorBrush(Color.Parse("#3344CC88"));
    private static readonly IBrush CHANGES_REQUESTED_BRUSH = new SolidColorBrush(Color.Parse("#33FF4444"));
    private static readonly IBrush REVIEW_REQUESTED_BRUSH = new SolidColorBrush(Color.Parse("#333388FF"));
    private static readonly IBrush NO_REVIEW_REQUESTED_BRUSH = new SolidColorBrush(Color.Parse("#33FF9800"));

    private readonly IBriefingRepository briefingRepository;
    private readonly IClipboardService clipboardService;
    private readonly IWatchedRepositoryStore repositoryStore;
    private readonly IAppSettingsStore settingsStore;
    private readonly PrSyncOrchestrator orchestrator;

    public BriefingCardViewModel(
        Briefing briefing,
        IBriefingRepository briefingRepository,
        IClipboardService clipboardService,
        IWatchedRepositoryStore repositoryStore,
        IAppSettingsStore settingsStore,
        PrSyncOrchestrator orchestrator)
    {
        Briefing = briefing;
        this.briefingRepository = briefingRepository;
        this.clipboardService = clipboardService;
        this.repositoryStore = repositoryStore;
        this.settingsStore = settingsStore;
        this.orchestrator = orchestrator;
        IsRead = briefing.IsRead;
    }

    public Briefing Briefing { get; private set; }

    public int PullRequestNumber => Briefing.PullRequestNumber;
    public string Title => Briefing.Title;
    public string Author => Briefing.Author;
    public string RepositoryDisplayName => Briefing.RepositoryDisplayName;
    public string Why => Briefing.Why ?? string.Empty;
    public IReadOnlyList<string> HighImpactFiles => Briefing.HighImpactFiles;
    public IReadOnlyList<RiskItem> TopRisks => Briefing.TopRisks;
    public bool HasAssessment => Briefing.GeneratedAtUtc.HasValue;
    public bool IsWellFormed => Briefing.IsWellFormed == false;
    public string PullRequestUrl => Briefing.PullRequestUrl;
    public DateTimeOffset? GeneratedAtUtc => Briefing.GeneratedAtUtc;
    public string GeneratedAtDisplay => Briefing.GeneratedAtUtc?.ToLocalTime().ToString("MMM d, h:mm tt") ?? "Not generated yet";

    private TimeSpan Age => DateTimeOffset.UtcNow - Briefing.CreatedAtUtc;

    public string AgeLabel => Age switch
    {
        _ when Age < NEW_THRESHOLD => "New",
        _ when Age < RECENT_THRESHOLD => "<14 days",
        _ when Age < AGING_THRESHOLD => "<1 month",
        _ => "Old"
    };

    public IBrush AgeBrush => Age switch
    {
        _ when Age < NEW_THRESHOLD => AGE_NEW_BRUSH,
        _ when Age < RECENT_THRESHOLD => AGE_RECENT_BRUSH,
        _ when Age < AGING_THRESHOLD => AGE_AGING_BRUSH,
        _ => AGE_OLD_BRUSH
    };

    // reviewDecision only reflects APPROVED/CHANGES_REQUESTED once someone has actually reviewed;
    // ReviewRequested (gh's reviewRequests) only lists *outstanding* requests — GitHub clears an
    // entry from it the moment that reviewer submits a review. So an approved PR has an empty
    // reviewRequests list too, and reviewDecision must be checked first or approved PRs would be
    // mislabeled as never having had a review requested at all.
    public string ReviewStatusLabel => Briefing switch
    {
        { IsDraft: true } => "Draft",
        { ReviewDecision: "APPROVED" } => "Approved",
        { ReviewDecision: "CHANGES_REQUESTED" } => "Changes Requested",
        { ReviewRequested: true } => "Review Requested",
        _ => "No Review Requested"
    };

    public IBrush ReviewStatusBrush => Briefing switch
    {
        { IsDraft: true } => DRAFT_BRUSH,
        { ReviewDecision: "APPROVED" } => APPROVED_BRUSH,
        { ReviewDecision: "CHANGES_REQUESTED" } => CHANGES_REQUESTED_BRUSH,
        { ReviewRequested: true } => REVIEW_REQUESTED_BRUSH,
        _ => NO_REVIEW_REQUESTED_BRUSH
    };

    [ObservableProperty]
    public partial bool IsRead { get; set; }

    [ObservableProperty]
    public partial string CopyLinkButtonLabel { get; set; } = COPY_LINK_LABEL;

    [RelayCommand]
    private async Task ToggleReadAsync()
    {
        IsRead = !IsRead;
        await briefingRepository.SetReadStateAsync(Briefing.RepositoryStorageKey, Briefing.PullRequestNumber, IsRead, CancellationToken.None);
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        if (string.IsNullOrWhiteSpace(PullRequestUrl))
            return;

        Process.Start(new ProcessStartInfo(PullRequestUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task CopyLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(PullRequestUrl))
            return;

        await clipboardService.SetTextAsync(PullRequestUrl);

        CopyLinkButtonLabel = COPY_LINK_CONFIRMATION_LABEL;
        await Task.Delay(TimeSpan.FromSeconds(2));
        CopyLinkButtonLabel = COPY_LINK_LABEL;
    }

    [ObservableProperty]
    public partial bool IsGeneratingAssessment { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAssessmentError))]
    public partial string? AssessmentErrorMessage { get; set; }

    public bool HasAssessmentError => !string.IsNullOrEmpty(AssessmentErrorMessage);

    [RelayCommand]
    private async Task GenerateAssessmentAsync()
    {
        if (IsGeneratingAssessment)
            return;

        IsGeneratingAssessment = true;
        AssessmentErrorMessage = null;
        try
        {
            IReadOnlyList<WatchedRepository> repositories = await repositoryStore.GetAllAsync(CancellationToken.None);
            WatchedRepository? repository = repositories.FirstOrDefault(r => r.StorageKey == Briefing.RepositoryStorageKey);
            if (repository is null)
            {
                AssessmentErrorMessage = "Repository is no longer being watched.";
                return;
            }

            AppSettings settings = await settingsStore.LoadAsync(CancellationToken.None);
            Briefing? updated = await orchestrator.GenerateAssessmentAsync(
                repository, Briefing.PullRequestNumber, settings.SelectedAgent, CancellationToken.None);

            if (updated is null)
            {
                AssessmentErrorMessage = "Failed to generate assessment. Check the Sync Log tab for details.";
                return;
            }

            UpdateBriefing(updated);
        }
        finally
        {
            IsGeneratingAssessment = false;
        }
    }

    private void UpdateBriefing(Briefing updated)
    {
        Briefing = updated;
        OnPropertyChanged(nameof(Why));
        OnPropertyChanged(nameof(HighImpactFiles));
        OnPropertyChanged(nameof(TopRisks));
        OnPropertyChanged(nameof(IsWellFormed));
        OnPropertyChanged(nameof(HasAssessment));
        OnPropertyChanged(nameof(GeneratedAtUtc));
        OnPropertyChanged(nameof(GeneratedAtDisplay));
    }
}
