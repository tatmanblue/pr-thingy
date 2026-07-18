using PrThingy.App.Services;
using PrThingy.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace PrThingy.App.DependencyInjection;

public static class AppServiceCollectionExtensions
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddSingleton<IFolderPickerService, FolderPickerService>();
        services.AddSingleton<IClipboardService, ClipboardService>();

        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<SyncLogViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services;
    }
}
