using PrThingy.Core.Models;

namespace PrThingy.Core.Abstractions;

public interface IAgentClient
{
    AgentType AgentType { get; }

    string CliFileName { get; }

    Task<AgentInvocationResult> GenerateBriefingAsync(
        string prompt,
        CancellationToken cancellationToken);
}

public sealed record AgentInvocationResult(
    bool Succeeded,
    string RawOutput,
    string? ErrorOutput,
    TimeSpan Duration);
