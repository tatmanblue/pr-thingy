using System.Diagnostics;
using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.Infrastructure.Agents;

/// <summary>
/// Shared invocation logic for agent CLIs that follow the `&lt;cli&gt; -p "&lt;prompt&gt;"`
/// non-interactive convention (confirmed for both `claude` and `gemini` at implementation time).
/// </summary>
public abstract class CliAgentClientBase(IProcessRunner processRunner) : IAgentClient
{
    private static readonly TimeSpan INVOCATION_TIMEOUT = TimeSpan.FromMinutes(5);

    public abstract string CliFileName { get; }

    public abstract AgentType AgentType { get; }

    public async Task<AgentInvocationResult> GenerateBriefingAsync(string prompt, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        ProcessRunResult result = await processRunner.RunAsync(
            new ProcessRunRequest(CliFileName, ["-p", prompt], Timeout: INVOCATION_TIMEOUT),
            cancellationToken);
        stopwatch.Stop();

        bool succeeded = !result.TimedOut && result.ExitCode == 0;
        string? errorOutput = result.TimedOut
            ? $"'{CliFileName}' timed out after {INVOCATION_TIMEOUT}"
            : result.ExitCode != 0 ? result.StandardError : null;

        return new AgentInvocationResult(succeeded, result.StandardOutput, errorOutput, stopwatch.Elapsed);
    }
}
