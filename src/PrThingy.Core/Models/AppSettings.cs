namespace PrThingy.Core.Models;

public sealed class AppSettings
{
    public AgentType SelectedAgent { get; set; } = AgentType.Claude;
    public int PollingIntervalMinutes { get; set; } = 60;
    public bool StartMinimizedToTray { get; set; } = true;
    public bool RunScanOnSettingsClose { get; set; } = true;
    public bool SyncOnStartup { get; set; } = true;
    public int MaxPullRequestsPerRepository { get; set; } = 30;

    // Null/empty means omit the CLI's --model flag and use its default.
    public string? AgentModel { get; set; }

    public AgentEffortLevel AgentEffort { get; set; } = AgentEffortLevel.Default;

    public int MaxDiffLengthChars { get; set; } = 60_000;
}
