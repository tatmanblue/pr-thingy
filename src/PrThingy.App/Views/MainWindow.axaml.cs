using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PrThingy.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace PrThingy.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.SyncLog.Entries.CollectionChanged += OnSyncLogEntriesChanged;
    }

    // Keeps the Sync Log tab pinned to the newest entry as syncs write to it live.
    private void OnSyncLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        SyncLogScrollViewer.ScrollToEnd();

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var settingsViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        var settingsWindow = new SettingsWindow { DataContext = settingsViewModel };
        _ = settingsViewModel.LoadCommand.ExecuteAsync(null);
        settingsWindow.ShowDialog(this);
    }
}
