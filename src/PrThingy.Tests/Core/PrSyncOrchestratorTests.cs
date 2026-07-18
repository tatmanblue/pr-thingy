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
        var agent = new Mock<IAgentClient>();
        agent.Setup(a => a.GenerateBriefingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentInvocationResult(true, rawOutput, null, TimeSpan.Zero));
        return agent;
    }

    [Fact]
    public async Task SyncRepositoryAsync_NewPullRequest_GeneratesAndSavesBriefing()
    {
        var repository = Repository();
        var pr = PullRequest(1, DateTimeOffset.UtcNow);

        var pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pr]);
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("diff");

        var briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        var agentClient = SucceedingAgentClient();
        var agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(AgentType.Claude)).Returns(agentClient.Object);

        var orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        var count = await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        Assert.Equal(1, count);
        briefingRepository.Verify(r => r.SaveAsync(It.Is<Briefing>(b => b.PullRequestNumber == 1 && b.Why == "ok"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncRepositoryAsync_UpToDateBriefingExists_SkipsRegeneration()
    {
        var repository = Repository();
        var updatedAt = DateTimeOffset.UtcNow;
        var pr = PullRequest(1, updatedAt);

        var existingBriefing = new Briefing
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

        var pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pr]);

        var briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBriefing);

        var agentClientFactory = new Mock<IAgentClientFactory>();

        var orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        var count = await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        Assert.Equal(0, count);
        pullRequestSource.Verify(s => s.GetDiffAsync(It.IsAny<WatchedRepository>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        briefingRepository.Verify(r => r.SaveAsync(It.IsAny<Briefing>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncRepositoryAsync_PullRequestUpdatedSinceLastBriefing_Regenerates()
    {
        var repository = Repository();
        var staleUpdatedAt = DateTimeOffset.UtcNow.AddDays(-1);
        var freshUpdatedAt = DateTimeOffset.UtcNow;
        var pr = PullRequest(1, freshUpdatedAt);

        var staleBriefing = new Briefing
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

        var pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pr]);
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("new diff");

        var briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleBriefing);

        var agentClient = SucceedingAgentClient("""{"why": "updated", "highImpactFiles": [], "topRisks": []}""");
        var agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(AgentType.Claude)).Returns(agentClient.Object);

        var orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        var count = await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        Assert.Equal(1, count);
        briefingRepository.Verify(r => r.SaveAsync(It.Is<Briefing>(b => b.Why == "updated"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncRepositoryAsync_AgentInvocationFails_SkipsWithoutSaving()
    {
        var repository = Repository();
        var pr = PullRequest(1, DateTimeOffset.UtcNow);

        var pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pr]);
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("diff");

        var briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        var agentClient = new Mock<IAgentClient>();
        agentClient.Setup(a => a.GenerateBriefingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentInvocationResult(false, string.Empty, "CLI not found", TimeSpan.Zero));

        var agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(AgentType.Claude)).Returns(agentClient.Object);

        var orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        var count = await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        Assert.Equal(0, count);
        briefingRepository.Verify(r => r.SaveAsync(It.IsAny<Briefing>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncRepositoryAsync_OnePullRequestThrows_DoesNotBlockOthers()
    {
        var repository = Repository();
        var failingPr = PullRequest(1, DateTimeOffset.UtcNow);
        var succeedingPr = PullRequest(2, DateTimeOffset.UtcNow);

        var pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([failingPr, succeedingPr]);
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("gh pr diff failed"));
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync("diff");

        var briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        var agentClient = SucceedingAgentClient();
        var agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(AgentType.Claude)).Returns(agentClient.Object);

        var orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        var count = await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        Assert.Equal(1, count);
        briefingRepository.Verify(r => r.SaveAsync(It.Is<Briefing>(b => b.PullRequestNumber == 2), It.IsAny<CancellationToken>()), Times.Once);
        briefingRepository.Verify(r => r.SaveAsync(It.Is<Briefing>(b => b.PullRequestNumber == 1), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncRepositoryAsync_NewPullRequest_CopiesCreatedAtDraftReviewRequestedAndReviewDecisionIntoBriefing()
    {
        var repository = Repository();
        var createdAt = DateTimeOffset.UtcNow.AddDays(-5);
        var pr = PullRequest(1, DateTimeOffset.UtcNow, createdAt: createdAt, isDraft: true, reviewRequested: false, reviewDecision: "APPROVED");

        var pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pr]);
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("diff");

        var briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        var agentClient = SucceedingAgentClient();
        var agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(AgentType.Claude)).Returns(agentClient.Object);

        var orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

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
        var repository = Repository();

        var pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var briefingRepository = new Mock<IBriefingRepository>();
        var agentClientFactory = new Mock<IAgentClientFactory>();

        var orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);
        var settings = new AppSettings { SelectedAgent = AgentType.Claude, MaxPullRequestsPerRepository = 45 };

        await orchestrator.SyncRepositoryAsync(repository, settings, CancellationToken.None);

        pullRequestSource.Verify(s => s.GetOpenPullRequestsAsync(repository, 45, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncRepositoryAsync_BriefingNoLongerOpenAndMerged_RemovesBriefing()
    {
        var repository = Repository();
        var closedBriefing = ExistingBriefing(repository, 1);

        var pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        pullRequestSource.Setup(s => s.GetPullRequestStateAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("MERGED");

        var briefingRepository = new Mock<IBriefingRepository>();
        var agentClientFactory = new Mock<IAgentClientFactory>();

        // BuildOrchestrator installs a default (empty) GetAllForRepositoryAsync setup; Moq gives
        // precedence to whichever matching setup was added last, so this override must come after.
        var orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);
        briefingRepository.Setup(r => r.GetAllForRepositoryAsync(repository.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([closedBriefing]);

        await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        briefingRepository.Verify(r => r.RemoveAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncRepositoryAsync_BriefingNoLongerOpenButNotMerged_KeepsBriefing()
    {
        var repository = Repository();
        var closedBriefing = ExistingBriefing(repository, 1);

        var pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        pullRequestSource.Setup(s => s.GetPullRequestStateAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("CLOSED");

        var briefingRepository = new Mock<IBriefingRepository>();
        var agentClientFactory = new Mock<IAgentClientFactory>();

        // See the comment in the sibling "…Merged…" test for why this setup order matters.
        var orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);
        briefingRepository.Setup(r => r.GetAllForRepositoryAsync(repository.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([closedBriefing]);

        await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        briefingRepository.Verify(r => r.RemoveAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
