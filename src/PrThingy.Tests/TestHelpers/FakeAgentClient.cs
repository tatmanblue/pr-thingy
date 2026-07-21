using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.Tests.TestHelpers;

public sealed class FakeAgentClient(AgentType agentType, Func<string, AgentInvocationResult> respond) : IAgentClient
{
    public AgentType AgentType => agentType;

    public string CliFileName => agentType.ToString().ToLowerInvariant();

    public int CallCount { get; private set; }

    public AgentInvocationOptions? LastOptions { get; private set; }

    public Task<AgentInvocationResult> GenerateBriefingAsync(
        string prompt, AgentInvocationOptions options, CancellationToken cancellationToken)
    {
        CallCount++;
        LastOptions = options;
        return Task.FromResult(respond(prompt));
    }
}
