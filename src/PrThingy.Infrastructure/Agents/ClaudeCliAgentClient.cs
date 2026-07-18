using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.Infrastructure.Agents;

public sealed class ClaudeCliAgentClient(IProcessRunner processRunner) : CliAgentClientBase(processRunner)
{
    protected override string CliFileName => "claude";

    public override AgentType AgentType => AgentType.Claude;
}
