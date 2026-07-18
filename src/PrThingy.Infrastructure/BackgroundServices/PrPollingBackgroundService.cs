using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;
using PrThingy.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PrThingy.Infrastructure.BackgroundServices;

public sealed class PrPollingBackgroundService(
    IAppSettingsStore settingsStore,
    IWatchedRepositoryStore repositoryStore,
    PrSyncOrchestrator orchestrator,
    ISyncLogService syncLog,
    ILogger<PrPollingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var isFirstRun = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = await settingsStore.LoadAsync(stoppingToken);
            var shouldSync = !isFirstRun || settings.SyncOnStartup;
            isFirstRun = false;

            if (shouldSync)
            {
                var repositories = await repositoryStore.GetAllAsync(stoppingToken);

                syncLog.SyncStarted();
                try
                {
                    foreach (var repository in repositories.Where(r => r.Enabled))
                    {
                        try
                        {
                            await orchestrator.SyncRepositoryAsync(repository, settings, stoppingToken);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logger.LogError(ex, "Sync failed for repository {RepositoryId}", repository.Id);
                            syncLog.Log(SyncLogLevel.Error, $"{repository.DisplayName}: sync failed — {ex.Message}");
                        }
                    }
                }
                finally
                {
                    syncLog.SyncFinished();
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(settings.PollingIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }
}
