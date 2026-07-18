using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.Tests.TestHelpers;

public sealed class FakePullRequestSource(IReadOnlyList<PullRequestSummary> pullRequests, string diff = "diff content") : IPullRequestSource
{
    public Task<IReadOnlyList<PullRequestSummary>> GetOpenPullRequestsAsync(WatchedRepository repository, int maxResults, CancellationToken cancellationToken) =>
        Task.FromResult(pullRequests);

    public Task<string> GetDiffAsync(WatchedRepository repository, int pullRequestNumber, CancellationToken cancellationToken) =>
        Task.FromResult(diff);

    public Task<string?> GetPullRequestStateAsync(WatchedRepository repository, int pullRequestNumber, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}
