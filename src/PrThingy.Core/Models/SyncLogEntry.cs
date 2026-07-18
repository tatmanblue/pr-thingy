namespace PrThingy.Core.Models;

public sealed record SyncLogEntry(DateTimeOffset TimestampUtc, SyncLogLevel Level, string Message);
