using PrThingy.Core.Abstractions;
using PrThingy.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace PrThingy.Core.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton<BriefingPromptBuilder>();
        services.AddSingleton<PrSyncOrchestrator>();
        services.AddSingleton<ISyncLogService, SyncLogService>();
        return services;
    }
}
