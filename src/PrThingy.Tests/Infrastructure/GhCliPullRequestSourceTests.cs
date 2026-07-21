using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;
using PrThingy.Infrastructure.GitHub;
using Moq;
using Xunit;

namespace PrThingy.Tests.Infrastructure;

public class GhCliPullRequestSourceTests
{
    private static WatchedRepository Repository() => WatchedRepository.Create("my-repo", "/tmp/my-repo");

    [Fact]
    public async Task GetOpenPullRequestsAsync_PassesMaxResultsAsGhLimitFlag()
    {
        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName == "git"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRunResult(0, string.Empty, string.Empty, false));
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName == "gh"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRunResult(0, "[]", string.Empty, false));

        GhCliPullRequestSource source = new GhCliPullRequestSource(processRunner.Object);

        await source.GetOpenPullRequestsAsync(Repository(), 45, CancellationToken.None);

        processRunner.Verify(p => p.RunAsync(
            It.Is<ProcessRunRequest>(r => r.FileName == "gh" && HasLimitArgument(r.Arguments, "45")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOpenPullRequestsAsync_RequestsHeadRefOidField()
    {
        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName == "git"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRunResult(0, string.Empty, string.Empty, false));
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName == "gh"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRunResult(0, "[]", string.Empty, false));

        GhCliPullRequestSource source = new GhCliPullRequestSource(processRunner.Object);

        await source.GetOpenPullRequestsAsync(Repository(), 30, CancellationToken.None);

        processRunner.Verify(p => p.RunAsync(
            It.Is<ProcessRunRequest>(r => r.FileName == "gh" && r.Arguments.Any(a => a.Split(',').Contains("headRefOid"))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static bool HasLimitArgument(IReadOnlyList<string> arguments, string expectedValue)
    {
        for (int i = 0; i < arguments.Count - 1; i++)
        {
            if (arguments[i] == "--limit" && arguments[i + 1] == expectedValue)
                return true;
        }

        return false;
    }

    [Fact]
    public async Task GetOpenPullRequestsAsync_MapsCreatedAtDraftReviewRequestsAndReviewDecision()
    {
        const string listJson = """
            [
              {"number": 1, "title": "Ready PR", "author": {"login": "octocat"}, "body": "", "url": "https://example.com/1",
               "updatedAt": "2026-07-01T00:00:00Z", "createdAt": "2026-06-01T00:00:00Z", "isDraft": false,
               "reviewRequests": [{"__typename": "User", "login": "reviewer1"}], "reviewDecision": "REVIEW_REQUIRED",
               "headRefOid": "abc123"},
              {"number": 2, "title": "Draft PR", "author": {"login": "octocat"}, "body": "", "url": "https://example.com/2",
               "updatedAt": "2026-07-02T00:00:00Z", "createdAt": "2026-06-02T00:00:00Z", "isDraft": true,
               "reviewRequests": [], "reviewDecision": null},
              {"number": 3, "title": "No reviewers PR", "author": {"login": "octocat"}, "body": "", "url": "https://example.com/3",
               "updatedAt": "2026-07-03T00:00:00Z", "createdAt": "2026-06-03T00:00:00Z", "isDraft": false,
               "reviewRequests": [], "reviewDecision": null},
              {"number": 4, "title": "Approved PR", "author": {"login": "octocat"}, "body": "", "url": "https://example.com/4",
               "updatedAt": "2026-07-04T00:00:00Z", "createdAt": "2026-06-04T00:00:00Z", "isDraft": false,
               "reviewRequests": [], "reviewDecision": "APPROVED"}
            ]
            """;

        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName == "git"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRunResult(0, string.Empty, string.Empty, false));
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName == "gh"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRunResult(0, listJson, string.Empty, false));

        GhCliPullRequestSource source = new GhCliPullRequestSource(processRunner.Object);

        IReadOnlyList<PullRequestSummary> results = await source.GetOpenPullRequestsAsync(Repository(), 30, CancellationToken.None);

        PullRequestSummary ready = results.Single(r => r.Number == 1);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), ready.CreatedAtUtc);
        Assert.False(ready.IsDraft);
        Assert.True(ready.ReviewRequested);
        Assert.Equal("REVIEW_REQUIRED", ready.ReviewDecision);
        Assert.Equal("abc123", ready.HeadCommitSha);

        PullRequestSummary draft = results.Single(r => r.Number == 2);
        Assert.True(draft.IsDraft);
        Assert.False(draft.ReviewRequested);
        Assert.Equal(string.Empty, draft.HeadCommitSha);

        PullRequestSummary noReviewers = results.Single(r => r.Number == 3);
        Assert.False(noReviewers.IsDraft);
        Assert.False(noReviewers.ReviewRequested);
        Assert.Null(noReviewers.ReviewDecision);

        // The #14580 case: everyone requested has already reviewed, so reviewRequests is empty
        // even though the PR was very much reviewed — reviewDecision is what actually shows that.
        PullRequestSummary approved = results.Single(r => r.Number == 4);
        Assert.False(approved.ReviewRequested);
        Assert.Equal("APPROVED", approved.ReviewDecision);
    }
}
