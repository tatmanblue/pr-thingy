using Avalonia.Media;
using PrThingy.Core.Models;

namespace PrThingy.App.ViewModels;

public sealed class SyncLogEntryViewModel(SyncLogEntry entry)
{
    public string TimestampDisplay => entry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");

    public string Message => entry.Message;

    public IBrush LevelBrush => entry.Level switch
    {
        SyncLogLevel.Warning => Brushes.Orange,
        SyncLogLevel.Error => Brushes.OrangeRed,
        _ => Brushes.Gray
    };
}
