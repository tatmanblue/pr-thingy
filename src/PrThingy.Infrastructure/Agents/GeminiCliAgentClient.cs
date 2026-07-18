using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.Infrastructure.Agents;

public sealed class GeminiCliAgentClient(IProcessRunner processRunner) : CliAgentClientBase(processRunner)
{
    protected override string CliFileName => "gemini";

    public override AgentType AgentType => AgentType.Gemini;
}
