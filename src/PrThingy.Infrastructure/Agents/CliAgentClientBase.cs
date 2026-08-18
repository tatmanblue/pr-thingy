using System.Diagnostics;
using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.Infrastructure.Agents;

/// <summary>
/// Shared invocation logic for agent CLIs that follow the `&lt;cli&gt; -p` non-interactive
/// convention (confirmed for both `claude` and `gemini` at implementation time), reading the
/// prompt from stdin rather than passing it as a command-line argument — a large prompt (a PR
/// description plus a sizeable diff) can exceed the OS command-line length limit otherwise,
/// which on Windows surfaces as a misleading "filename or extension is too long" error.
/// </summary>
public abstract class CliAgentClientBase(IProcessRunner processRunner) : IAgentClient
{
    private static readonly TimeSpan INVOCATION_TIMEOUT = TimeSpan.FromMinutes(5);

    // Loose, case-insensitive heuristic: exact wording varies by CLI and version, and we've
    // never captured the literal text of an expired-login failure from either CLI. Any failure
    // that doesn't match still falls back to the generic error path below, so a missed phrase
    // here only costs a less-specific message rather than a broken one.
    private static readonly string[] AUTHENTICATION_FAILURE_MARKERS =
    [
        "not authenticated",
        "not logged in",
        "please log in",
        "please run",
        "/login",
        "unauthorized",
        "invalid api key",
        "authentication required",
        "authentication failed",
        "token expired",
        "session expired",
        "please sign in"
    ];

    public abstract string CliFileName { get; }

    public abstract AgentType AgentType { get; }

    protected abstract IEnumerable<string> BuildOptionArguments(AgentInvocationOptions options);

    public async Task<AgentInvocationResult> GenerateBriefingAsync(
        string prompt, AgentInvocationOptions options, CancellationToken cancellationToken)
    {
        List<string> arguments = ["-p", ..BuildOptionArguments(options)];

        Stopwatch stopwatch = Stopwatch.StartNew();
        ProcessRunResult result = await processRunner.RunAsync(
            new ProcessRunRequest(CliFileName, arguments, StandardInput: prompt, Timeout: INVOCATION_TIMEOUT),
            cancellationToken);
        stopwatch.Stop();

        bool succeeded = !result.TimedOut && result.ExitCode == 0;
        string? errorOutput = result.TimedOut
            ? $"'{CliFileName}' timed out after {INVOCATION_TIMEOUT}"
            : result.ExitCode != 0 ? result.StandardError : null;
        bool isAuthenticationFailure = !succeeded && !result.TimedOut && LooksLikeAuthenticationFailure(result.StandardError);

        return new AgentInvocationResult(succeeded, result.StandardOutput, errorOutput, stopwatch.Elapsed, isAuthenticationFailure);
    }

    private static bool LooksLikeAuthenticationFailure(string? stderr) =>
        !string.IsNullOrEmpty(stderr)
        && AUTHENTICATION_FAILURE_MARKERS.Any(marker => stderr.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
