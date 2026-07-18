using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Input;
using PrThingy.App.ViewModels;

namespace PrThingy.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly TrayIcon trayIcon;

    public TrayIconService(Window mainWindow, MainWindowViewModel viewModel, Action onExit)
    {
        var icon = new WindowIcon(AssetLoader.Open(new Uri("avares://PrThingy.App/Assets/avalonia-logo.ico")));

        trayIcon = new TrayIcon
        {
            Icon = icon,
            ToolTipText = "PR Thingy — pre-review PR dashboard",
            IsVisible = true,
            Menu = new NativeMenu
            {
                new NativeMenuItem("Open Dashboard") { Command = new RelayCommand(() => ShowWindow(mainWindow)) },
                new NativeMenuItem("Sync Now") { Command = viewModel.Dashboard.SyncNowCommand },
                new NativeMenuItemSeparator(),
                new NativeMenuItem("Exit") { Command = new RelayCommand(onExit) }
            }
        };

        trayIcon.Clicked += (_, _) => ShowWindow(mainWindow);
    }

    private static void ShowWindow(Window window)
    {
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    public void Dispose() => trayIcon.Dispose();
}
