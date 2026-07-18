using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using PrThingy.App.ViewModels;
using PrThingy.App.Views;
using Microsoft.Extensions.DependencyInjection;

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

            var mainWindowViewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow { DataContext = mainWindowViewModel };

            // Closing the main window quits the app (background sync included) — no tray/background
            // mode for now. See design.md's "Invisible Assistant" concept for the deferred alternative.
            desktop.MainWindow = mainWindow;

            _ = mainWindowViewModel.Dashboard.LoadCommand.ExecuteAsync(null);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
