namespace PrThingy.Core.Models;

// Represents a tracked open PR. The PR-metadata fields are always populated by sync;
// the assessment fields (Why/HighImpactFiles/TopRisks/GeneratedAtUtc/GeneratedByAgent/
// IsWellFormed) stay null until the agent has been run for this PR via GenerateAssessmentAsync.
public sealed record Briefing
{
    public required string RepositoryStorageKey { get; init; }
    public required string RepositoryDisplayName { get; init; }
    public required int PullRequestNumber { get; init; }
    public required string Title { get; init; }
    public required string Author { get; init; }
    public required string PullRequestUrl { get; init; }

    // Not required: briefings persisted before this field existed must keep loading, defaulting
    // to an empty description. Needed to rebuild the agent prompt on-demand without an extra gh call.
    public string Body { get; init; } = string.Empty;

    // Not required: briefings persisted before this field existed must keep loading, defaulting
    // to DateTimeOffset.MinValue. Refreshed on every sync regardless of assessment state.
    public DateTimeOffset UpdatedAtUtc { get; init; }

    public string? Why { get; init; }
    public IReadOnlyList<string> HighImpactFiles { get; init; } = [];
    public IReadOnlyList<RiskItem> TopRisks { get; init; } = [];
    public DateTimeOffset? GeneratedAtUtc { get; init; }
    public AgentType? GeneratedByAgent { get; init; }

    // Not required: briefings persisted before these fields existed must keep loading, defaulting
    // to CreatedAtUtc = DateTimeOffset.MinValue (bucketed as "Old") and the other flags to false.
    public DateTimeOffset CreatedAtUtc { get; init; }
    public bool IsDraft { get; init; }
    public bool ReviewRequested { get; init; }
    public string? ReviewDecision { get; init; }

    /// <summary>
    /// Null until an assessment has been generated. False when the agent's raw output could not
    /// be parsed into the expected JSON shape — Why then holds the raw output verbatim and
    /// HighImpactFiles/TopRisks are empty.
    /// </summary>
    public bool? IsWellFormed { get; init; }

    public bool IsRead { get; set; }
}
