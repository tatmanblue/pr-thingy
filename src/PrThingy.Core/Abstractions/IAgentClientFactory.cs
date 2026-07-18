using PrThingy.Core.Models;

namespace PrThingy.Core.Abstractions;

public interface IAgentClientFactory
{
    IAgentClient GetClient(AgentType agentType);
}
