using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;
using Microsoft.Extensions.Logging;

namespace PrThingy.Core.Services;

public sealed class PrSyncOrchestrator(
    IPullRequestSource pullRequestSource,
    IAgentClientFactory agentClientFactory,
    IBriefingRepository briefingRepository,
    BriefingPromptBuilder promptBuilder,
    ISyncLogService syncLog,
    ILogger<PrSyncOrchestrator> logger)
{
    public async Task<int> SyncRepositoryAsync(WatchedRepository repository, AppSettings settings, CancellationToken cancellationToken)
    {
        var pullRequests = await pullRequestSource.GetOpenPullRequestsAsync(
            repository, settings.MaxPullRequestsPerRepository, cancellationToken);
        syncLog.Log(SyncLogLevel.Info, $"{repository.DisplayName}: found {pullRequests.Count} open PR(s)");

        var newOrUpdatedCount = 0;

        foreach (var pullRequest in pullRequests)
        {
            var existing = await briefingRepository.GetAsync(repository.StorageKey, pullRequest.Number, cancellationToken);
            if (existing is not null && existing.SourcePullRequestUpdatedAtUtc >= pullRequest.UpdatedAtUtc)
                continue;

            try
            {
                syncLog.Log(SyncLogLevel.Info, $"{repository.DisplayName} #{pullRequest.Number}: generating briefing");

                var diff = await pullRequestSource.GetDiffAsync(repository, pullRequest.Number, cancellationToken);
                var prompt = promptBuilder.Build(repository, pullRequest, diff);
                var client = agentClientFactory.GetClient(settings.SelectedAgent);
                var result = await client.GenerateBriefingAsync(prompt, cancellationToken);

                if (!result.Succeeded)
                {
                    logger.LogWarning(
                        "Agent invocation failed for {Repository} PR #{PullRequestNumber}: {Error}",
                        repository.DisplayName, pullRequest.Number, result.ErrorOutput);
                    syncLog.Log(SyncLogLevel.Warning,
                        $"{repository.DisplayName} #{pullRequest.Number}: agent invocation failed — {result.ErrorOutput}");
                    continue;
                }

                var parsed = AgentResponseParser.Parse(result.RawOutput);
                var briefing = new Briefing
                {
                    RepositoryStorageKey = repository.StorageKey,
                    RepositoryDisplayName = repository.DisplayName,
                    PullRequestNumber = pullRequest.Number,
                    Title = pullRequest.Title,
                    Author = pullRequest.Author,
                    PullRequestUrl = pullRequest.Url,
                    Why = parsed.Why,
                    HighImpactFiles = parsed.HighImpactFiles,
                    TopRisks = parsed.TopRisks,
                    GeneratedAtUtc = DateTimeOffset.UtcNow,
                    SourcePullRequestUpdatedAtUtc = pullRequest.UpdatedAtUtc,
                    GeneratedByAgent = settings.SelectedAgent,
                    IsWellFormed = parsed.IsWellFormed,
                    IsRead = false,
                    CreatedAtUtc = pullRequest.CreatedAtUtc,
                    IsDraft = pullRequest.IsDraft,
                    ReviewRequested = pullRequest.ReviewRequested,
                    ReviewDecision = pullRequest.ReviewDecision
                };

                await briefingRepository.SaveAsync(briefing, cancellationToken);
                newOrUpdatedCount++;
                syncLog.Log(SyncLogLevel.Info, $"{repository.DisplayName} #{pullRequest.Number}: briefing saved");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Failed to sync {Repository} PR #{PullRequestNumber}",
                    repository.DisplayName, pullRequest.Number);
                syncLog.Log(SyncLogLevel.Error, $"{repository.DisplayName} #{pullRequest.Number}: sync failed — {ex.Message}");
            }
        }

        await RemoveMergedBriefingsAsync(repository, pullRequests, cancellationToken);

        return newOrUpdatedCount;
    }

    // PRs that fall out of the open list have either been merged or closed without merging.
    // Only merged ones are pruned — a closed-without-merge briefing might still be worth a look.
    private async Task RemoveMergedBriefingsAsync(
        WatchedRepository repository, IReadOnlyList<PullRequestSummary> openPullRequests, CancellationToken cancellationToken)
    {
        var openNumbers = openPullRequests.Select(p => p.Number).ToHashSet();
        var existingBriefings = await briefingRepository.GetAllForRepositoryAsync(repository.StorageKey, cancellationToken);

        foreach (var briefing in existingBriefings)
        {
            if (openNumbers.Contains(briefing.PullRequestNumber))
                continue;

            string? state;
            try
            {
                state = await pullRequestSource.GetPullRequestStateAsync(repository, briefing.PullRequestNumber, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Failed to check state of {Repository} PR #{PullRequestNumber}",
                    repository.DisplayName, briefing.PullRequestNumber);
                continue;
            }

            if (!string.Equals(state, "MERGED", StringComparison.OrdinalIgnoreCase))
                continue;

            await briefingRepository.RemoveAsync(repository.StorageKey, briefing.PullRequestNumber, cancellationToken);
            syncLog.Log(SyncLogLevel.Info, $"{repository.DisplayName} #{briefing.PullRequestNumber}: merged — removed");
        }
    }
}
