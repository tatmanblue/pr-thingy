using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.Infrastructure.Agents;

public sealed class ClaudeCliAgentClient(IProcessRunner processRunner) : CliAgentClientBase(processRunner)
{
    public override string CliFileName => "claude";

    public override AgentType AgentType => AgentType.Claude;

    protected override IEnumerable<string> BuildOptionArguments(AgentInvocationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Model))
        {
            yield return "--model";
            yield return options.Model;
        }

        if (options.Effort != AgentEffortLevel.Default)
        {
            yield return "--effort";
            yield return options.Effort.ToString().ToLowerInvariant();
        }
    }
}
