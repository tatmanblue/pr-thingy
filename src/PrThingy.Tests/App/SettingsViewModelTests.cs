using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PrThingy.App.Services;
using PrThingy.App.ViewModels;
using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;
using PrThingy.Core.Services;
using Xunit;

namespace PrThingy.Tests.App;

public class SettingsViewModelTests
{
    private static SettingsViewModel BuildViewModel(
        IFolderPickerService folderPickerService,
        out Mock<IWatchedRepositoryStore> repositoryStore,
        out Mock<IBriefingRepository> briefingRepository)
    {
        repositoryStore = new Mock<IWatchedRepositoryStore>();
        repositoryStore
            .Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WatchedRepository>());

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
        settingsStore
            .Setup(s => s.SaveAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IPullRequestSource> pullRequestSource = new Mock<IPullRequestSource>();
        Mock<IAgentClient> agentClient = new Mock<IAgentClient>();
        Mock<IAgentClientFactory> agentClientFactory = new Mock<IAgentClientFactory>();
        agentClientFactory.Setup(f => f.GetClient(It.IsAny<AgentType>())).Returns(agentClient.Object);

        SyncLogService syncLog = new SyncLogService();
        PrSyncOrchestrator orchestrator = new PrSyncOrchestrator(
            pullRequestSource.Object,
            agentClientFactory.Object,
            briefingRepository.Object,
            new BriefingPromptBuilder(),
            syncLog,
            NullLogger<PrSyncOrchestrator>.Instance);

        DashboardViewModel dashboard = new DashboardViewModel(
            briefingRepository.Object,
            repositoryStore.Object,
            settingsStore.Object,
            orchestrator,
            syncLog,
            Mock.Of<IClipboardService>());

        return new SettingsViewModel(
            repositoryStore.Object,
            briefingRepository.Object,
            settingsStore.Object,
            folderPickerService,
            dashboard);
    }

    [Fact]
    public async Task AddRepositoryAsync_SameFolderAlreadyWatched_DoesNotAddDuplicateRow()
    {
        Mock<IFolderPickerService> folderPicker = new Mock<IFolderPickerService>();
        folderPicker
            .SetupSequence(f => f.PickFolderAsync())
            .ReturnsAsync(@"C:\repos\myrepo")
            .ReturnsAsync(@"C:\repos\myrepo\");

        SettingsViewModel viewModel = BuildViewModel(folderPicker.Object, out _, out _);

        await viewModel.AddRepositoryCommand.ExecuteAsync(null);
        await viewModel.AddRepositoryCommand.ExecuteAsync(null);

        WatchedRepositoryRowViewModel row = Assert.Single(viewModel.Repositories);
        Assert.Equal(@"C:\repos\myrepo", row.LocalPath);
        Assert.False(string.IsNullOrEmpty(viewModel.StatusMessage));
    }

    [Fact]
    public async Task AddRepositoryAsync_DifferentFolders_AddsBothRows()
    {
        Mock<IFolderPickerService> folderPicker = new Mock<IFolderPickerService>();
        folderPicker
            .SetupSequence(f => f.PickFolderAsync())
            .ReturnsAsync(@"C:\repos\repo-a")
            .ReturnsAsync(@"C:\repos\repo-b");

        SettingsViewModel viewModel = BuildViewModel(folderPicker.Object, out _, out _);

        await viewModel.AddRepositoryCommand.ExecuteAsync(null);
        await viewModel.AddRepositoryCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Repositories.Count);
    }

    [Fact]
    public async Task SaveAsync_RemovedRepository_DeletesItsBriefingData()
    {
        WatchedRepository repository = WatchedRepository.Create("myrepo", @"C:\repos\myrepo");

        Mock<IFolderPickerService> folderPicker = new Mock<IFolderPickerService>();
        SettingsViewModel viewModel = BuildViewModel(
            folderPicker.Object, out Mock<IWatchedRepositoryStore> repositoryStore, out Mock<IBriefingRepository> briefingRepository);
        repositoryStore
            .Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([repository]);

        await viewModel.LoadCommand.ExecuteAsync(null);
        WatchedRepositoryRowViewModel row = Assert.Single(viewModel.Repositories);
        viewModel.RemoveRepositoryCommand.Execute(row);

        await viewModel.SaveCommand.ExecuteAsync(null);

        repositoryStore.Verify(s => s.RemoveAsync(repository.Id, It.IsAny<CancellationToken>()), Times.Once);
        briefingRepository.Verify(
            r => r.DeleteAllForRepositoryAsync(repository.StorageKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
