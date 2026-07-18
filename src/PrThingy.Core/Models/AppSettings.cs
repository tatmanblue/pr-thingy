namespace PrThingy.Core.Models;

public sealed class AppSettings
{
    public AgentType SelectedAgent { get; set; } = AgentType.Claude;
    public int PollingIntervalMinutes { get; set; } = 60;
    public bool StartMinimizedToTray { get; set; } = true;
    public bool RunScanOnSettingsClose { get; set; } = true;
    public bool SyncOnStartup { get; set; } = true;
    public int MaxPullRequestsPerRepository { get; set; } = 30;
}
