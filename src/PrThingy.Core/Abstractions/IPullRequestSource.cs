using PrThingy.Core.Models;

namespace PrThingy.Core.Abstractions;

public interface IPullRequestSource
{
    Task<IReadOnlyList<PullRequestSummary>> GetOpenPullRequestsAsync(
        WatchedRepository repository, int maxResults, CancellationToken cancellationToken);

    Task<string> GetDiffAsync(
        WatchedRepository repository, int pullRequestNumber, CancellationToken cancellationToken);

    // Returns "OPEN", "CLOSED", "MERGED", or null if the state could not be determined.
    Task<string?> GetPullRequestStateAsync(
        WatchedRepository repository, int pullRequestNumber, CancellationToken cancellationToken);
}
