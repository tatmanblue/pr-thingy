using Avalonia.Controls;
using Avalonia.Interactivity;
using PrThingy.App.ViewModels;

namespace PrThingy.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
            viewModel.CloseRequested += Close;
    }

    private void OnRemoveRepositoryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WatchedRepositoryRowViewModel row } && DataContext is SettingsViewModel viewModel)
        {
            viewModel.RemoveRepositoryCommand.Execute(row);
        }
    }
}
