using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrThingy.Core.Abstractions;

namespace PrThingy.App.ViewModels;

public partial class MainWindowViewModel(
    DashboardViewModel dashboard,
    SyncLogViewModel syncLog,
    IStartupEnvironmentChecker startupEnvironmentChecker) : ViewModelBase
{
    public DashboardViewModel Dashboard { get; } = dashboard;

    public SyncLogViewModel SyncLog { get; } = syncLog;

    [ObservableProperty]
    public partial string? StartupWarningMessage { get; set; }

    [ObservableProperty]
    public partial bool IsStartupWarningVisible { get; set; }

    [RelayCommand]
    private async Task CheckStartupEnvironmentAsync()
    {
        string? warning = await startupEnvironmentChecker.GetStartupWarningAsync(CancellationToken.None);
        StartupWarningMessage = warning;
        IsStartupWarningVisible = warning is not null;
    }

    [RelayCommand]
    private void DismissStartupWarning() => IsStartupWarningVisible = false;
}
