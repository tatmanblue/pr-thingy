namespace PrThingy.Core.Models;

public sealed class Briefing
{
    public required string RepositoryStorageKey { get; init; }
    public required string RepositoryDisplayName { get; init; }
    public required int PullRequestNumber { get; init; }
    public required string Title { get; init; }
    public required string Author { get; init; }
    public required string PullRequestUrl { get; init; }
    public required string Why { get; init; }
    public required IReadOnlyList<string> HighImpactFiles { get; init; }
    public required IReadOnlyList<RiskItem> TopRisks { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }

    /// <summary>
    /// The PR's `updatedAt` at the time this briefing was generated. Compared against the
    /// PR's current `updatedAt` on each sync to decide whether the briefing is stale and
    /// needs regenerating.
    /// </summary>
    public required DateTimeOffset SourcePullRequestUpdatedAtUtc { get; init; }

    public required AgentType GeneratedByAgent { get; init; }

    // Not required: briefings persisted before these fields existed must keep loading, defaulting
    // to CreatedAtUtc = DateTimeOffset.MinValue (bucketed as "Old") and the other flags to false.
    public DateTimeOffset CreatedAtUtc { get; init; }
    public bool IsDraft { get; init; }
    public bool ReviewRequested { get; init; }
    public string? ReviewDecision { get; init; }

    /// <summary>
    /// False when the agent's raw output could not be parsed into the expected JSON shape —
    /// Why then holds the raw output verbatim and HighImpactFiles/TopRisks are empty.
    /// </summary>
    public required bool IsWellFormed { get; init; }

    public bool IsRead { get; set; }
}
