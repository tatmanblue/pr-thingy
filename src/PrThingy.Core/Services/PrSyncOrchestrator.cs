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
            try
            {
                Briefing? existing = await briefingRepository.GetAsync(repository.StorageKey, pullRequest.Number, cancellationToken);

                Briefing record = existing is null
                    ? new Briefing
                    {
                        RepositoryStorageKey = repository.StorageKey,
                        RepositoryDisplayName = repository.DisplayName,
                        PullRequestNumber = pullRequest.Number,
                        Title = pullRequest.Title,
                        Author = pullRequest.Author,
                        Body = pullRequest.Body,
                        PullRequestUrl = pullRequest.Url,
                        CreatedAtUtc = pullRequest.CreatedAtUtc,
                        UpdatedAtUtc = pullRequest.UpdatedAtUtc,
                        IsDraft = pullRequest.IsDraft,
                        ReviewRequested = pullRequest.ReviewRequested,
                        ReviewDecision = pullRequest.ReviewDecision
                    }
                    : existing with
                    {
                        RepositoryDisplayName = repository.DisplayName,
                        Title = pullRequest.Title,
                        Author = pullRequest.Author,
                        Body = pullRequest.Body,
                        PullRequestUrl = pullRequest.Url,
                        CreatedAtUtc = pullRequest.CreatedAtUtc,
                        UpdatedAtUtc = pullRequest.UpdatedAtUtc,
                        IsDraft = pullRequest.IsDraft,
                        ReviewRequested = pullRequest.ReviewRequested,
                        ReviewDecision = pullRequest.ReviewDecision
                    };

                await briefingRepository.SaveAsync(record, cancellationToken);
                if (existing is null)
                    newOrUpdatedCount++;
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

    public async Task<Briefing?> GenerateAssessmentAsync(
        WatchedRepository repository, int pullRequestNumber, AppSettings settings, CancellationToken cancellationToken)
    {
        Briefing? existing = await briefingRepository.GetAsync(repository.StorageKey, pullRequestNumber, cancellationToken);
        if (existing is null)
        {
            logger.LogWarning(
                "No tracked PR record found for {Repository} PR #{PullRequestNumber}; cannot generate assessment",
                repository.DisplayName, pullRequestNumber);
            syncLog.Log(SyncLogLevel.Warning,
                $"{repository.DisplayName} #{pullRequestNumber}: no tracked PR record found — sync first");
            return null;
        }

        try
        {
            syncLog.Log(SyncLogLevel.Info, $"{repository.DisplayName} #{pullRequestNumber}: generating assessment");

            string diff = await pullRequestSource.GetDiffAsync(repository, pullRequestNumber, cancellationToken);
            PullRequestSummary pullRequest = new PullRequestSummary
            {
                Number = existing.PullRequestNumber,
                Title = existing.Title,
                Author = existing.Author,
                Body = existing.Body,
                Url = existing.PullRequestUrl,
                UpdatedAtUtc = existing.UpdatedAtUtc,
                CreatedAtUtc = existing.CreatedAtUtc,
                IsDraft = existing.IsDraft,
                ReviewRequested = existing.ReviewRequested,
                ReviewDecision = existing.ReviewDecision
            };
            string prompt = promptBuilder.Build(repository, pullRequest, diff, settings.MaxDiffLengthChars);
            IAgentClient client = agentClientFactory.GetClient(settings.SelectedAgent);
            AgentInvocationOptions options = new AgentInvocationOptions(settings.AgentModel, settings.AgentEffort);
            AgentInvocationResult result = await client.GenerateBriefingAsync(prompt, options, cancellationToken);

            if (!result.Succeeded)
            {
                logger.LogWarning(
                    "Agent invocation failed for {Repository} PR #{PullRequestNumber}: {Error}",
                    repository.DisplayName, pullRequestNumber, result.ErrorOutput);
                syncLog.Log(SyncLogLevel.Warning,
                    $"{repository.DisplayName} #{pullRequestNumber}: agent invocation failed — {result.ErrorOutput}");
                return null;
            }

            ParsedBriefingContent parsed = AgentResponseParser.Parse(result.RawOutput);
            Briefing updated = existing with
            {
                Why = parsed.Why,
                HighImpactFiles = parsed.HighImpactFiles,
                TopRisks = parsed.TopRisks,
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                GeneratedByAgent = settings.SelectedAgent,
                IsWellFormed = parsed.IsWellFormed
            };

            await briefingRepository.SaveAsync(updated, cancellationToken);
            syncLog.Log(SyncLogLevel.Info, $"{repository.DisplayName} #{pullRequestNumber}: assessment saved");
            return updated;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Failed to generate assessment for {Repository} PR #{PullRequestNumber}",
                repository.DisplayName, pullRequestNumber);
            syncLog.Log(SyncLogLevel.Error, $"{repository.DisplayName} #{pullRequestNumber}: assessment generation failed — {ex.Message}");
            return null;
        }
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
