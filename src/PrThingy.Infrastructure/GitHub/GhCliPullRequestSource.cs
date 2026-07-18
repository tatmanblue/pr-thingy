using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.Infrastructure.GitHub;

public sealed class GhCliPullRequestSource(IProcessRunner processRunner) : IPullRequestSource
{
    private static readonly TimeSpan CLI_TIMEOUT = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<PullRequestSummary>> GetOpenPullRequestsAsync(
        WatchedRepository repository, int maxResults, CancellationToken cancellationToken)
    {
        // Read-only: updates remote-tracking refs only, does not touch the working tree.
        await processRunner.RunAsync(
            new ProcessRunRequest("git", ["-C", repository.LocalPath, "fetch", "origin"], Timeout: CLI_TIMEOUT),
            cancellationToken);

        ProcessRunResult listResult = await processRunner.RunAsync(
            new ProcessRunRequest(
                "gh",
                ["pr", "list", "--json", "number,title,author,body,updatedAt,url,createdAt,isDraft,reviewRequests,reviewDecision", "--limit", maxResults.ToString(CultureInfo.InvariantCulture)],
                WorkingDirectory: repository.LocalPath,
                Timeout: CLI_TIMEOUT),
            cancellationToken);

        if (listResult.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'gh pr list' failed for repository '{repository.DisplayName}': {listResult.StandardError}");
        }

        List<GhPullRequestListEntry> entries = JsonSerializer.Deserialize<List<GhPullRequestListEntry>>(listResult.StandardOutput, JsonOptions) ?? [];

        return entries
            .Select(entry => new PullRequestSummary
            {
                Number = entry.Number,
                Title = entry.Title ?? string.Empty,
                Author = entry.Author?.Login ?? "unknown",
                Body = entry.Body ?? string.Empty,
                Url = entry.Url ?? string.Empty,
                UpdatedAtUtc = entry.UpdatedAt,
                CreatedAtUtc = entry.CreatedAt,
                IsDraft = entry.IsDraft,
                ReviewRequested = entry.ReviewRequests is { Count: > 0 },
                ReviewDecision = entry.ReviewDecision
            })
            .ToList();
    }

    public async Task<string> GetDiffAsync(
        WatchedRepository repository, int pullRequestNumber, CancellationToken cancellationToken)
    {
        ProcessRunResult result = await processRunner.RunAsync(
            new ProcessRunRequest(
                "gh",
                ["pr", "diff", pullRequestNumber.ToString(CultureInfo.InvariantCulture)],
                WorkingDirectory: repository.LocalPath,
                Timeout: CLI_TIMEOUT),
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'gh pr diff {pullRequestNumber}' failed for repository '{repository.DisplayName}': {result.StandardError}");
        }

        return result.StandardOutput;
    }

    public async Task<string?> GetPullRequestStateAsync(
        WatchedRepository repository, int pullRequestNumber, CancellationToken cancellationToken)
    {
        ProcessRunResult result = await processRunner.RunAsync(
            new ProcessRunRequest(
                "gh",
                ["pr", "view", pullRequestNumber.ToString(CultureInfo.InvariantCulture), "--json", "state"],
                WorkingDirectory: repository.LocalPath,
                Timeout: CLI_TIMEOUT),
            cancellationToken);

        if (result.ExitCode != 0)
            return null;

        GhPullRequestStateEntry? entry = JsonSerializer.Deserialize<GhPullRequestStateEntry>(result.StandardOutput, JsonOptions);
        return entry?.State;
    }

    private sealed class GhPullRequestStateEntry
    {
        public string? State { get; set; }
    }

    private sealed class GhPullRequestListEntry
    {
        public int Number { get; set; }
        public string? Title { get; set; }
        public GhAuthor? Author { get; set; }
        public string? Body { get; set; }
        public string? Url { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTimeOffset UpdatedAt { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("isDraft")]
        public bool IsDraft { get; set; }

        [JsonPropertyName("reviewRequests")]
        public List<JsonElement>? ReviewRequests { get; set; }

        [JsonPropertyName("reviewDecision")]
        public string? ReviewDecision { get; set; }
    }

    private sealed class GhAuthor
    {
        public string? Login { get; set; }
    }
}
