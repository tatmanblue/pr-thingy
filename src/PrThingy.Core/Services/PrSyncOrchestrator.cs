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
        IReadOnlyList<PullRequestSummary> pullRequests = await pullRequestSource.GetOpenPullRequestsAsync(
            repository, settings.MaxPullRequestsPerRepository, cancellationToken);
        syncLog.Log(SyncLogLevel.Info, $"{repository.DisplayName}: found {pullRequests.Count} open PR(s)");

        int newOrUpdatedCount = 0;

        foreach (PullRequestSummary pullRequest in pullRequests)
        {
            Briefing? existing = await briefingRepository.GetAsync(repository.StorageKey, pullRequest.Number, cancellationToken);
            if (existing is not null && existing.SourcePullRequestUpdatedAtUtc >= pullRequest.UpdatedAtUtc)
                continue;

            try
            {
                syncLog.Log(SyncLogLevel.Info, $"{repository.DisplayName} #{pullRequest.Number}: generating briefing");

                string diff = await pullRequestSource.GetDiffAsync(repository, pullRequest.Number, cancellationToken);
                string prompt = promptBuilder.Build(repository, pullRequest, diff);
                IAgentClient client = agentClientFactory.GetClient(settings.SelectedAgent);
                AgentInvocationResult result = await client.GenerateBriefingAsync(prompt, cancellationToken);

                if (!result.Succeeded)
                {
                    logger.LogWarning(
                        "Agent invocation failed for {Repository} PR #{PullRequestNumber}: {Error}",
                        repository.DisplayName, pullRequest.Number, result.ErrorOutput);
                    syncLog.Log(SyncLogLevel.Warning,
                        $"{repository.DisplayName} #{pullRequest.Number}: agent invocation failed — {result.ErrorOutput}");
                    continue;
                }

                ParsedBriefingContent parsed = AgentResponseParser.Parse(result.RawOutput);
                Briefing briefing = new Briefing
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
        HashSet<int> openNumbers = openPullRequests.Select(p => p.Number).ToHashSet();
        IReadOnlyList<Briefing> existingBriefings = await briefingRepository.GetAllForRepositoryAsync(repository.StorageKey, cancellationToken);

        foreach (Briefing briefing in existingBriefings)
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
