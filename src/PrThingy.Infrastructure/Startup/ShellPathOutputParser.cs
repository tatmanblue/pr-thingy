namespace PrThingy.Infrastructure.Startup;

public static class ShellPathOutputParser
{
    public const string PATH_START_MARKER = "__PRTHINGY_PATH_START__";
    public const string PATH_END_MARKER = "__PRTHINGY_PATH_END__";

    public static string BuildProbeCommand()
        => $"echo \"{PATH_START_MARKER}${{PATH}}{PATH_END_MARKER}\"";

    public static string? ExtractPath(string shellOutput)
    {
        int startIndex = shellOutput.IndexOf(PATH_START_MARKER, StringComparison.Ordinal);
        if (startIndex < 0)
            return null;

        startIndex += PATH_START_MARKER.Length;
        int endIndex = shellOutput.IndexOf(PATH_END_MARKER, startIndex, StringComparison.Ordinal);
        if (endIndex < 0)
            return null;

        string extractedPath = shellOutput[startIndex..endIndex].Trim();
        return extractedPath.Length > 0 ? extractedPath : null;
    }
}
