namespace PrThingy.App.ViewModels;

public partial class MainWindowViewModel(DashboardViewModel dashboard, SyncLogViewModel syncLog) : ViewModelBase
{
    public DashboardViewModel Dashboard { get; } = dashboard;

    public SyncLogViewModel SyncLog { get; } = syncLog;
}
