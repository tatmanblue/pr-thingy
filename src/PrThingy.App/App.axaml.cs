using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using PrThingy.App.ViewModels;
using PrThingy.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PrThingy.App;

public partial class App : Application
{
    private readonly IServiceProvider? serviceProvider;

    public static IServiceProvider Services { get; private set; } = null!;

    // Parameterless constructor required by the Avalonia XAML previewer/loader.
    public App()
    {
    }

    public App(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        Services = serviceProvider;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && serviceProvider is not null)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            MainWindowViewModel mainWindowViewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
            MainWindow mainWindow = new MainWindow { DataContext = mainWindowViewModel };

            // Closing the main window quits the app (background sync included) — no tray/background
            // mode for now. See design.md's "Invisible Assistant" concept for the deferred alternative.
            desktop.MainWindow = mainWindow;

            _ = RunFireAndForgetAsync(mainWindowViewModel.Dashboard.LoadCommand.ExecuteAsync(null), serviceProvider);
            _ = RunFireAndForgetAsync(mainWindowViewModel.CheckStartupEnvironmentCommand.ExecuteAsync(null), serviceProvider);
        }

        base.OnFrameworkInitializationCompleted();
    }

    // The [RelayCommand]-generated ExecuteAsync task is otherwise discarded here, so a fault
    // would only surface as an unobserved task exception. Log it instead.
    private static async Task RunFireAndForgetAsync(Task task, IServiceProvider serviceProvider)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            serviceProvider.GetRequiredService<ILogger<App>>().LogError(ex, "Startup command failed");
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        DataAnnotationsValidationPlugin[] dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (DataAnnotationsValidationPlugin? plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
