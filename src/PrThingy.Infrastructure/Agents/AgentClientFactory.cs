using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace PrThingy.Infrastructure.Agents;

public sealed class AgentClientFactory(IServiceProvider serviceProvider) : IAgentClientFactory
{
    public IAgentClient GetClient(AgentType agentType) =>
        serviceProvider.GetRequiredKeyedService<IAgentClient>(agentType);
}
