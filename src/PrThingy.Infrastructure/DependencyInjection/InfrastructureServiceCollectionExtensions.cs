using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;
using PrThingy.Infrastructure.Agents;
using PrThingy.Infrastructure.BackgroundServices;
using PrThingy.Infrastructure.Startup;
using PrThingy.Infrastructure.GitHub;
using PrThingy.Infrastructure.Processes;
using PrThingy.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PrThingy.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        AppPaths.EnsureDirectoriesExist();

        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IStartupEnvironmentChecker, StartupEnvironmentChecker>();
        services.AddSingleton<LoginShellPathResolver>();
        services.AddSingleton<MacOsPathEnvironmentFixer>();
        services.AddSingleton<IPullRequestSource, GhCliPullRequestSource>();

        services.AddKeyedSingleton<IAgentClient, ClaudeCliAgentClient>(AgentType.Claude);
        services.AddKeyedSingleton<IAgentClient, GeminiCliAgentClient>(AgentType.Gemini);
        services.AddSingleton<IAgentClientFactory, AgentClientFactory>();

        services.AddSingleton<IBriefingRepository>(_ => new FileBriefingRepository(AppPaths.BriefingsDirectory));
        services.AddSingleton<IWatchedRepositoryStore>(_ => new FileWatchedRepositoryStore(AppPaths.RepositoriesFilePath));
        services.AddSingleton<IAppSettingsStore>(_ => new FileAppSettingsStore(AppPaths.SettingsFilePath));

        services.AddHostedService<PrPollingBackgroundService>();

        return services;
    }
}
