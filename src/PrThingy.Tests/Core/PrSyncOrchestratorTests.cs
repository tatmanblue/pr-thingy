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

    private static Briefing ExistingBriefing(WatchedRepository repository, int number, bool withAssessment = true) => new()
    {
        RepositoryStorageKey = repository.StorageKey,
        RepositoryDisplayName = repository.DisplayName,
        PullRequestNumber = number,
        Title = $"PR {number}",
        Author = "octocat",
        Body = "body",
        PullRequestUrl = $"https://example.com/pr/{number}",
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
        Why = withAssessment ? "already summarized" : null,
        HighImpactFiles = [],
        TopRisks = [],
        GeneratedAtUtc = withAssessment ? DateTimeOffset.UtcNow : null,
        GeneratedByAgent = withAssessment ? AgentType.Claude : null,
        IsWellFormed = withAssessment ? true : null
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
        agent.Setup(a => a.GenerateBriefingAsync(It.IsAny<string>(), It.IsAny<AgentInvocationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentInvocationResult(true, rawOutput, null, TimeSpan.Zero));
        return agent;
    }

    [Fact]
    public async Task SyncRepositoryAsync_NewPullRequest_SavesUnassessedRecord()
    {
        WatchedRepository repository = Repository();
        PullRequestSummary pr = PullRequest(1, DateTimeOffset.UtcNow);

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pr]);

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        int count = await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        Assert.Equal(1, count);
        briefingRepository.Verify(r => r.SaveAsync(
            It.Is<Briefing>(b => b.PullRequestNumber == 1 && b.Why == null && b.GeneratedAtUtc == null),
            It.IsAny<CancellationToken>()), Times.Once);
        pullRequestSource.Verify(s => s.GetDiffAsync(It.IsAny<WatchedRepository>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        agentClientFactory.Verify(f => f.GetClient(It.IsAny<AgentType>()), Times.Never);
    }

    [Fact]
    public async Task SyncRepositoryAsync_ExistingUnassessedRecord_RefreshesMetadataWithoutCallingAgent()
    {
        WatchedRepository repository = Repository();
        PullRequestSummary pr = PullRequest(1, DateTimeOffset.UtcNow, reviewDecision: "APPROVED");

        Briefing existing = ExistingBriefing(repository, 1, withAssessment: false);

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pr]);

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        int count = await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        Assert.Equal(0, count);
        briefingRepository.Verify(r => r.SaveAsync(
            It.Is<Briefing>(b => b.ReviewDecision == "APPROVED" && b.Why == null),
            It.IsAny<CancellationToken>()), Times.Once);
        agentClientFactory.Verify(f => f.GetClient(It.IsAny<AgentType>()), Times.Never);
    }

    [Fact]
    public async Task SyncRepositoryAsync_ExistingAssessedRecord_PreservesAssessmentFields()
    {
        WatchedRepository repository = Repository();
        PullRequestSummary pr = PullRequest(1, DateTimeOffset.UtcNow);

        Briefing existing = ExistingBriefing(repository, 1, withAssessment: true);

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetOpenPullRequestsAsync(repository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pr]);

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        briefingRepository.Verify(r => r.SaveAsync(
            It.Is<Briefing>(b => b.Why == "already summarized" && b.GeneratedAtUtc == existing.GeneratedAtUtc),
            It.IsAny<CancellationToken>()), Times.Once);
        agentClientFactory.Verify(f => f.GetClient(It.IsAny<AgentType>()), Times.Never);
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

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("disk read failed"));
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();

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

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        await orchestrator.SyncRepositoryAsync(repository, DefaultSettings(), CancellationToken.None);

        briefingRepository.Verify(r => r.SaveAsync(
            It.Is<Briefing>(b => b.CreatedAtUtc == createdAt && b.IsDraft && !b.ReviewRequested && b.ReviewDecision == "APPROVED"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

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

    [Fact]
    public async Task GenerateAssessmentAsync_ExistingRecord_GeneratesAndSavesAssessment()
    {
        WatchedRepository repository = Repository();
        Briefing existing = ExistingBriefing(repository, 1, withAssessment: false);

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("diff");

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Mock<IAgentClient> agentClient = SucceedingAgentClient();
        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(AgentType.Claude)).Returns(agentClient.Object);

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        Briefing? result = await orchestrator.GenerateAssessmentAsync(repository, 1, DefaultSettings(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("ok", result!.Why);
        Assert.NotNull(result.GeneratedAtUtc);
        Assert.Equal(AgentType.Claude, result.GeneratedByAgent);
        briefingRepository.Verify(r => r.SaveAsync(It.Is<Briefing>(b => b.Why == "ok"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAssessmentAsync_ForwardsModelAndEffortFromSettings()
    {
        WatchedRepository repository = Repository();
        Briefing existing = ExistingBriefing(repository, 1, withAssessment: false);

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("diff");

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        AgentInvocationOptions? capturedOptions = null;
        Mock<IAgentClient> agentClient = new Mock<IAgentClient>();
        agentClient
            .Setup(a => a.GenerateBriefingAsync(It.IsAny<string>(), It.IsAny<AgentInvocationOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, AgentInvocationOptions, CancellationToken>((_, options, _) => capturedOptions = options)
            .ReturnsAsync(new AgentInvocationResult(true, """{"why": "ok", "highImpactFiles": [], "topRisks": []}""", null, TimeSpan.Zero));

        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(AgentType.Claude)).Returns(agentClient.Object);

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);
        AppSettings settings = new AppSettings
        {
            SelectedAgent = AgentType.Claude,
            AgentModel = "haiku",
            AgentEffort = AgentEffortLevel.Low
        };

        await orchestrator.GenerateAssessmentAsync(repository, 1, settings, CancellationToken.None);

        Assert.NotNull(capturedOptions);
        Assert.Equal("haiku", capturedOptions!.Model);
        Assert.Equal(AgentEffortLevel.Low, capturedOptions.Effort);
    }

    [Fact]
    public async Task GenerateAssessmentAsync_ForwardsMaxDiffLengthCharsToPromptBuilder()
    {
        WatchedRepository repository = Repository();
        Briefing existing = ExistingBriefing(repository, 1, withAssessment: false);

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new string('x', 1000));

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        string? capturedPrompt = null;
        Mock<IAgentClient> agentClient = new Mock<IAgentClient>();
        agentClient
            .Setup(a => a.GenerateBriefingAsync(It.IsAny<string>(), It.IsAny<AgentInvocationOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, AgentInvocationOptions, CancellationToken>((prompt, _, _) => capturedPrompt = prompt)
            .ReturnsAsync(new AgentInvocationResult(true, """{"why": "ok", "highImpactFiles": [], "topRisks": []}""", null, TimeSpan.Zero));

        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(AgentType.Claude)).Returns(agentClient.Object);

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);
        AppSettings settings = new AppSettings { SelectedAgent = AgentType.Claude, MaxDiffLengthChars = 100 };

        await orchestrator.GenerateAssessmentAsync(repository, 1, settings, CancellationToken.None);

        Assert.NotNull(capturedPrompt);
        Assert.Contains("[diff truncated", capturedPrompt);
    }

    [Fact]
    public async Task GenerateAssessmentAsync_NoExistingRecord_ReturnsNullWithoutCallingAgent()
    {
        WatchedRepository repository = Repository();

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        Briefing? result = await orchestrator.GenerateAssessmentAsync(repository, 1, DefaultSettings(), CancellationToken.None);

        Assert.Null(result);
        agentClientFactory.Verify(f => f.GetClient(It.IsAny<AgentType>()), Times.Never);
        briefingRepository.Verify(r => r.SaveAsync(It.IsAny<Briefing>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAssessmentAsync_AgentInvocationFails_ReturnsNullAndPreservesPriorAssessment()
    {
        WatchedRepository repository = Repository();
        Briefing existing = ExistingBriefing(repository, 1, withAssessment: true);

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource.Setup(s => s.GetDiffAsync(repository, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("diff");

        Mock<IBriefingRepository> briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository.Setup(r => r.GetAsync(repository.StorageKey, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Mock<IAgentClient> agentClient = new Mock<IAgentClient>();
        agentClient.Setup(a => a.GenerateBriefingAsync(It.IsAny<string>(), It.IsAny<AgentInvocationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentInvocationResult(false, string.Empty, "CLI not found", TimeSpan.Zero));

        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(AgentType.Claude)).Returns(agentClient.Object);

        PrSyncOrchestrator orchestrator = BuildOrchestrator(pullRequestSource, agentClientFactory, briefingRepository);

        Briefing? result = await orchestrator.GenerateAssessmentAsync(repository, 1, DefaultSettings(), CancellationToken.None);

        Assert.Null(result);
        briefingRepository.Verify(r => r.SaveAsync(It.IsAny<Briefing>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
