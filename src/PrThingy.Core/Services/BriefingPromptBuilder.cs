using PrThingy.Core.Models;

namespace PrThingy.Core.Services;

public sealed class BriefingPromptBuilder
{
    private const int MAX_DIFF_LENGTH_CHARS = 60_000;

    private const string JSON_SCHEMA_EXAMPLE =
        """{"why": "one or two sentence summary of the PR's intent", "highImpactFiles": ["path/one", "path/two"], "topRisks": [{"file": "path/to/file", "line": 42, "description": "risk description"}]}""";

    public string Build(
        WatchedRepository repository, PullRequestSummary pullRequest, string diff, int maxDiffLengthChars = MAX_DIFF_LENGTH_CHARS)
    {
        string truncatedDiff = diff.Length > maxDiffLengthChars
            ? diff[..maxDiffLengthChars] + "\n\n[diff truncated — exceeded " + maxDiffLengthChars + " characters]"
            : diff;

        return $"""
            You are a code-review prep assistant. Analyze the following pull request from the
            repository "{repository.DisplayName}" and respond with ONLY a single JSON object in
            this exact shape — no markdown code fences, no commentary before or after:

            {JSON_SCHEMA_EXAMPLE}

            - "why": explain the intent behind the change in plain English.
            - "highImpactFiles": list the most significant files changed (paths relative to repo root).
            - "topRisks": list up to 3 specific technical risks, bugs, or edge cases a reviewer should watch
              for. For each, set "file" (path relative to repo root) and "line" to the exact spot in the
              diff the risk is about — use the line number from the NEW version of the file (the right-hand,
              "+" side of the diff hunk), since that's what a reviewer sees in GitHub's PR view. If a risk
              isn't tied to one specific location, set "file" and "line" to null.

            PR #{pullRequest.Number}: {pullRequest.Title}
            Author: {pullRequest.Author}

            Description:
            {pullRequest.Body}

            --- CODE CHANGES ---
            {truncatedDiff}
            """;
    }
}
