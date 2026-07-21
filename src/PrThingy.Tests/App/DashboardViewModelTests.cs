using PrThingy.App.Services;
using PrThingy.App.ViewModels;
using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;
using PrThingy.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace PrThingy.Tests.App;

public class DashboardViewModelTests
{
    private static WatchedRepository Repository(string displayName) => WatchedRepository.Create(displayName, "/tmp/" + displayName);

    private static DashboardViewModel BuildViewModel(
        Mock<IPullRequestSource> pullRequestSource,
        Mock<IWatchedRepositoryStore> repositoryStore,
        SyncLogService syncLog,
        out Mock<IBriefingRepository> briefingRepository)
    {
        briefingRepository = new Mock<IBriefingRepository>();
        briefingRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Briefing>());
        briefingRepository
            .Setup(r => r.GetAllForRepositoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Briefing>());
        briefingRepository
            .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        Mock<IAppSettingsStore> settingsStore = new Mock<IAppSettingsStore>();
        settingsStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppSettings { SelectedAgent = AgentType.Claude });

        Mock<IAgentClient> agentClient = new Mock<IAgentClient>();
        agentClient
            .Setup(a => a.GenerateBriefingAsync(It.IsAny<string>(), It.IsAny<AgentInvocationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentInvocationResult(true, """{"why": "ok", "highImpactFiles": [], "topRisks": []}""", null, TimeSpan.Zero));
        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(AgentType.Claude)).Returns(agentClient.Object);

        PrSyncOrchestrator orchestrator = new PrSyncOrchestrator(
            pullRequestSource.Object,
            agentClientFactory.Object,
            briefingRepository.Object,
            new BriefingPromptBuilder(),
            syncLog,
            NullLogger<PrSyncOrchestrator>.Instance);

        return new DashboardViewModel(
            briefingRepository.Object,
            repositoryStore.Object,
            settingsStore.Object,
            orchestrator,
            syncLog,
            Mock.Of<IClipboardService>());
    }

    [Fact]
    public async Task SyncNowAsync_OneRepositoryThrows_LogsErrorAndSyncsRemainingRepositories()
    {
        WatchedRepository failingRepository = Repository("failing-repo");
        WatchedRepository succeedingRepository = Repository("succeeding-repo");

        Mock<IWatchedRepositoryStore> repositoryStore = new Mock<IWatchedRepositoryStore>();
        repositoryStore
            .Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([failingRepository, succeedingRepository]);

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        pullRequestSource
            .Setup(s => s.GetOpenPullRequestsAsync(failingRepository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "'gh pr list' failed for repository 'failing-repo': gh auth login"));
        pullRequestSource
            .Setup(s => s.GetOpenPullRequestsAsync(succeedingRepository, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PullRequestSummary>());

        SyncLogService syncLog = new SyncLogService();
        DashboardViewModel viewModel = BuildViewModel(pullRequestSource, repositoryStore, syncLog, out _);

        await viewModel.SyncNowCommand.ExecuteAsync(null);

        Assert.Contains(syncLog.Entries, e => e.Level == SyncLogLevel.Error && e.Message.Contains("failing-repo") && e.Message.Contains("gh auth login"));
        pullRequestSource.Verify(
            s => s.GetOpenPullRequestsAsync(succeedingRepository, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.False(syncLog.IsSyncing);
    }
}
