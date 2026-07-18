using System.Text;

namespace PrThingy.Core.Models;

public sealed class WatchedRepository
{
    /// <summary>Stable identity, assigned once at creation and never changed.</summary>
    public required string Id { get; init; }

    /// <summary>
    /// Slug derived from DisplayName + a fragment of Id at creation time, used as the
    /// briefings/&lt;StorageKey&gt;/ folder name. Immutable after creation so renaming
    /// DisplayName later doesn't orphan existing briefing files.
    /// </summary>
    public required string StorageKey { get; init; }

    public required string DisplayName { get; set; }
    public required string LocalPath { get; set; }
    public bool Enabled { get; set; } = true;

    public static WatchedRepository Create(string displayName, string localPath)
    {
        string id = Guid.NewGuid().ToString();
        return new WatchedRepository
        {
            Id = id,
            StorageKey = $"{Slugify(displayName)}-{id[..8]}",
            DisplayName = displayName,
            LocalPath = localPath
        };
    }

    private static string Slugify(string value)
    {
        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char c in value.ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(c) ? c : '-');

        return builder.ToString().Trim('-');
    }
}
