using PrThingy.Core.Models;
using PrThingy.Core.Services;
using Xunit;

namespace PrThingy.Tests.Core;

public class BriefingPromptBuilderTests
{
    private readonly BriefingPromptBuilder builder = new();

    private static WatchedRepository Repository() => WatchedRepository.Create("my-repo", "/tmp/my-repo");

    private static PullRequestSummary PullRequest() => new()
    {
        Number = 42,
        Title = "Add feature",
        Author = "octocat",
        Body = "This PR adds a feature.",
        Url = "https://github.com/example/repo/pull/42",
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    [Fact]
    public void Build_IncludesSchemaInstructionsAndPullRequestDetails()
    {
        string prompt = builder.Build(Repository(), PullRequest(), "diff --git a/x.cs b/x.cs");

        Assert.Contains("ONLY a single JSON object", prompt);
        Assert.Contains("\"why\"", prompt);
        Assert.Contains("\"highImpactFiles\"", prompt);
        Assert.Contains("\"topRisks\"", prompt);
        Assert.Contains("PR #42", prompt);
        Assert.Contains("Add feature", prompt);
        Assert.Contains("octocat", prompt);
        Assert.Contains("diff --git a/x.cs b/x.cs", prompt);
    }

    [Fact]
    public void Build_OversizedDiff_TruncatesWithNote()
    {
        string hugeDiff = new string('x', 70_000);

        string prompt = builder.Build(Repository(), PullRequest(), hugeDiff);

        Assert.Contains("[diff truncated", prompt);
        Assert.DoesNotContain(new string('x', 70_000), prompt);
    }

    [Fact]
    public void Build_SmallDiff_IsNotTruncated()
    {
        string smallDiff = "diff --git a/x.cs b/x.cs\n+added line";

        string prompt = builder.Build(Repository(), PullRequest(), smallDiff);

        Assert.DoesNotContain("truncated", prompt);
        Assert.Contains(smallDiff, prompt);
    }
}
