namespace PrThingy.Infrastructure.Startup;

public static class PathEnvironmentMerger
{
    // Shell-resolved entries take precedence (so Homebrew/nvm/asdf shims are found first),
    // but the original entries are kept as a trailing safety net in case the shell PATH is
    // missing something the restricted default provided.
    public static string Merge(string? shellResolvedPath, string? currentPath)
    {
        if (string.IsNullOrWhiteSpace(shellResolvedPath))
            return currentPath ?? string.Empty;

        List<string> orderedEntries = [];
        HashSet<string> seenEntries = new HashSet<string>();

        foreach (string entry in SplitEntries(shellResolvedPath).Concat(SplitEntries(currentPath)))
        {
            if (seenEntries.Add(entry))
                orderedEntries.Add(entry);
        }

        return string.Join(Path.PathSeparator, orderedEntries);
    }

    private static IEnumerable<string> SplitEntries(string? path)
        => (path ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
}
