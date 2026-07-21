namespace PrThingy.Core.Models;

public sealed class PullRequestSummary
{
    public required int Number { get; init; }
    public required string Title { get; init; }
    public required string Author { get; init; }
    public required string Body { get; init; }
    public required string Url { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public bool IsDraft { get; init; }

    /// <summary>Whether one or more reviewers/teams currently have an outstanding review request.</summary>
    public bool ReviewRequested { get; init; }

    /// <summary>Raw GitHub reviewDecision: "APPROVED", "CHANGES_REQUESTED", "REVIEW_REQUIRED", or null.</summary>
    public string? ReviewDecision { get; init; }

    /// <summary>The PR branch's current head commit SHA (gh's headRefOid); used to detect new pushes.</summary>
    public string HeadCommitSha { get; init; } = string.Empty;
}
