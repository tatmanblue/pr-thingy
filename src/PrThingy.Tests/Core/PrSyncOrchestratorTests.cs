using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;
using PrThingy.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace PrThingy.Tests.Core;

public class PrSyncOrchestratorTests
{
    private static WatchedRepository Repository() => WatchedRepository.Create("my-repo", "/tmp/my-repo");

    private static AppSettings DefaultSettings() => new() { SelectedAgent = AgentType.Claude };

    private static PullRequestSummary PullRequest(
        int number, DateTimeOffset updatedAt, DateTimeOffset? createdAt = null, bool isDraft = false,
        bool reviewRequested = false, string? reviewDecision = null) => new()
    {
        Number = number,
        Title = $"PR {number}",
        Author = "octocat",
        Body = "body",
        Url = $"https://example.com/pr/{number}",
        UpdatedAtUtc = updatedAt,
        CreatedAtUtc = createdAt ?? updatedAt,
        IsDraft = isDraft,
        ReviewRequested = reviewRequested,
        ReviewDecision = reviewDecision
    };

    private static PrSyncOrchestrator BuildOrchestrator(
        Mock<IPullRequestSource> pullRequestSource,
        Mock<IAgentClientFactory> agentClientFactory,
        Mock<IBriefingRepository> briefingRepository)
    {
        // Reconciliation always calls this — default to "nothing else stored" unless a test overrides it.
        briefingRepository
            .Setup(r => r.GetAllForRepositoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Briefing>());

        return new(
            pullRequestSource.Object,
            agentClientFactory.Object,
            briefingRepository.Object,
            new BriefingPromptBuilder(),
            new SyncLogService(),
            NullLogger<PrSyncOrchestrator>.Instance);
    }

    private static Mock<IAgentClient> SucceedingAgentClient(string rawOutput = """{"why": "ok", "highImpactFiles": [], "topRisks": []}""")
    {
        Mock<IAgentClient> agent = new Mock<IAgentClient>();
        agent.Setup(a => a.GenerateBriefingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentInvocationResult(true, rawOutput, null, TimeSpan.Zero));
        return agent;
    }

    [Fact]
    public async Task SyncRepositoryAsync_NewPullRequest_GeneratesAndSavesBriefing()
    {
        WatchedRepository repository = Repository();
        PullRequestSummary pr = PullRequest(1, DateTimeOffset.UtcNow);

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pr]);
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("diff");

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        Mock<IAgentClient> agentClient = SucceedingAgentClient();
        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(AgentType.Claude)).Returns(agentClient.Object);

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        int count = await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        Assert.Equal(1, count);
        briefingRepository.Verify(r => r.SaveAsync(It.Is<Briefing>(b => b.PullRequestNumber == 1 && b.Why == "ok"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncRepositoryAsync_UpToDateBriefingExists_SkipsRegeneration()
    {
        WatchedRepository repository = Repository();
        DateTimeOffset updatedAt = DateTimeOffset.UtcNow;
        PullRequestSummary pr = PullRequest(1, updatedAt);

        Briefing existingBriefing = new Briefing
        {
            RepositoryStorageKey = repository.StorageKey,
            RepositoryDisplayName = repository.DisplayName,
            PullRequestNumber = 1,
            Title = pr.Title,
            Author = pr.Author,
            PullRequestUrl = pr.Url,
            Why = "already summarized",
            HighImpactFiles = [],
            TopRisks = [],
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            SourcePullRequestUpdatedAtUtc = updatedAt,
            GeneratedByAgent = AgentType.Claude,
            IsWellFormed = true
        };

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pr]);

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBriefing);

        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        int count = await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        Assert.Equal(0, count);
        pullRequestSource.Verify(s => s.GetDiffAsync(It.IsAny<WatchedRepository>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        briefingRepository.Verify(r => r.SaveAsync(It.IsAny<Briefing>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncRepositoryAsync_PullRequestUpdatedSinceLastBriefing_Regenerates()
    {
        WatchedRepository repository = Repository();
        DateTimeOffset staleUpdatedAt = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset freshUpdatedAt = DateTimeOffset.UtcNow;
        PullRequestSummary pr = PullRequest(1, freshUpdatedAt);

        Briefing staleBriefing = new Briefing
        {
            RepositoryStorageKey = repository.StorageKey,
            RepositoryDisplayName = repository.DisplayName,
            PullRequestNumber = 1,
            Title = pr.Title,
            Author = pr.Author,
            PullRequestUrl = pr.Url,
            Why = "stale",
            HighImpactFiles = [],
            TopRisks = [],
            GeneratedAtUtc = staleUpdatedAt,
            SourcePullRequestUpdatedAtUtc = staleUpdatedAt,
            GeneratedByAgent = AgentType.Claude,
            IsWellFormed = true
        };

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pr]);
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("new diff");

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleBriefing);

        Mock<IAgentClient> agentClient = SucceedingAgentClient("""{"why": "updated", "highImpactFiles": [], "topRisks": []}""");
        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(AgentType.Claude)).Returns(agentClient.Object);

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        int count = await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        Assert.Equal(1, count);
        briefingRepository.Verify(r => r.SaveAsync(It.Is<Briefing>(b => b.Why == "updated"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncRepositoryAsync_AgentInvocationFails_SkipsWithoutSaving()
    {
        WatchedRepository repository = Repository();
        PullRequestSummary pr = PullRequest(1, DateTimeOffset.UtcNow);

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pr]);
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("diff");

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        Mock<IAgentClient> agentClient = new Mock<IAgentClient>();
        agentClient.Setup(a => a.GenerateBriefingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentInvocationResult(false, string.Empty, "CLI not found", TimeSpan.Zero));

        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(AgentType.Claude)).Returns(agentClient.Object);

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        int count = await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        Assert.Equal(0, count);
        briefingRepository.Verify(r => r.SaveAsync(It.IsAny<Briefing>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncRepositoryAsync_OnePullRequestThrows_DoesNotBlockOthers()
    {
        WatchedRepository repository = Repository();
        PullRequestSummary failingPr = PullRequest(1, DateTimeOffset.UtcNow);
        PullRequestSummary succeedingPr = PullRequest(2, DateTimeOffset.UtcNow);

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([failingPr, succeedingPr]);
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("gh pr diff failed"));
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync("diff");

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        Mock<IAgentClient> agentClient = SucceedingAgentClient();
        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(AgentType.Claude)).Returns(agentClient.Object);

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        int count = await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        Assert.Equal(1, count);
        briefingRepository.Verify(r => r.SaveAsync(It.Is<Briefing>(b => b.PullRequestNumber == 2), It.IsAny<CancellationToken>()), Times.Once);
        briefingRepository.Verify(r => r.SaveAsync(It.Is<Briefing>(b => b.PullRequestNumber == 1), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncRepositoryAsync_NewPullRequest_CopiesCreatedAtDraftReviewRequestedAndReviewDecisionIntoBriefing()
    {
        WatchedRepository repository = Repository();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow.AddDays(-5);
        PullRequestSummary pr = PullRequest(1, DateTimeOffset.UtcNow, createdAt: createdAt, isDraft: true, reviewRequested: false, reviewDecision: "APPROVED");

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pr]);
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("diff");

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        Mock<IAgentClient> agentClient = SucceedingAgentClient();
        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(AgentType.Claude)).Returns(agentClient.Object);

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        briefingRepository.Verify(r => r.SaveAsync(
            It.Is<Briefing>(b => b.CreatedAtUtc == createdAt && b.IsDraft && !b.ReviewRequested && b.ReviewDecision == "APPROVED"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Briefing ExistingBriefing(WatchedRepository repository, int number) => new()
    {
        RepositoryStorageKey = repository.StorageKey,
        RepositoryDisplayName = repository.DisplayName,
        PullRequestNumber = number,
        Title = $"PR {number}",
        Author = "octocat",
        PullRequestUrl = $"https://example.com/pr/{number}",
        Why = "already summarized",
        HighImpactFiles = [],
        TopRisks = [],
        GeneratedAtUtc = DateTimeOffset.UtcNow,
        SourcePullRequestUpdatedAtUtc = DateTimeOffset.UtcNow,
        GeneratedByAgent = AgentType.Claude,
        IsWellFormed = true
    };

    [Fact]
    public async Task SyncRepositoryAsync_PassesMaxPullRequestsPerRepositoryThrough()
    {
        WatchedRepository repository = Repository();

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);
        AppSettings settings = new AppSettings { SelectedAgent = AgentType.Claude, MaxPullRequestsPerRepository = 45 };

        await orchestrator.SyncRepositoryAsync(repository, settings, CancellationToken.None);

        pullRequestSource.Verify(s => s.GetOpenPullRequestsAsync(repository, 45, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncRepositoryAsync_BriefingNoLongerOpenAndMerged_RemovesBriefing()
    {
        WatchedRepository repository = Repository();
        Briefing closedBriefing = ExistingBriefing(repository, 1);

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        pullRequestSource.Setup(s => s.GetPullRequestStateAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("MERGED");

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();

        // BuildOrchestrator installs a default (empty) GetAllForRepositoryAsync setup; Moq gives
        // precedence to whichever matching setup was added last, so this override must come after.
        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);
        briefingRepository.Setup(r => r.GetAllForRepositoryAsync(repository.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([closedBriefing]);

        await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        briefingRepository.Verify(r => r.RemoveAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncRepositoryAsync_BriefingNoLongerOpenButNotMerged_KeepsBriefing()
    {
        WatchedRepository repository = Repository();
        Briefing closedBriefing = ExistingBriefing(repository, 1);

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        pullRequestSource.Setup(s => s.GetPullRequestStateAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("CLOSED");

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();

        // See the comment in the sibling "…Merged…" test for why this setup order matters.
        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);
        briefingRepository.Setup(r => r.GetAllForRepositoryAsync(repository.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([closedBriefing]);

        await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        briefingRepository.Verify(r => r.RemoveAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
