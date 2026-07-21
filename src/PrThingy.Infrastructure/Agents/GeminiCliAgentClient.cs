using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.Infrastructure.Agents;

public sealed class GeminiCliAgentClient(IProcessRunner processRunner) : CliAgentClientBase(processRunner)
{
    public override string CliFileName => "gemini";

    public override AgentType AgentType => AgentType.Gemini;

    protected override IEnumerable<string> BuildOptionArguments(AgentInvocationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Model))
        {
            yield return "--model";
            yield return options.Model;
        }

        // gemini-cli's effort/thinking-budget equivalent (if any) couldn't be verified on this
        // machine — gemini isn't installed here to check --help. Left unmapped rather than
        // guessing a flag name that could break the invocation; Effort is Claude-only for now.
    }
}
