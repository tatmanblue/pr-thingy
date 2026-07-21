using PrThingy.Infrastructure.Startup;
using Xunit;

namespace PrThingy.Tests.Infrastructure;

public class ShellPathOutputParserTests
{
    [Fact]
    public void ExtractPath_ValidMarkers_ReturnsPathBetweenThem()
    {
        string output = $"{ShellPathOutputParser.PATH_START_MARKER}/usr/local/bin:/usr/bin{ShellPathOutputParser.PATH_END_MARKER}";

        string? path = ShellPathOutputParser.ExtractPath(output);

        Assert.Equal("/usr/local/bin:/usr/bin", path);
    }

    [Fact]
    public void ExtractPath_BannerTextBeforeMarkers_IgnoresBanner()
    {
        string output = $"Welcome to zsh\nMessage of the day...\n{ShellPathOutputParser.PATH_START_MARKER}/opt/homebrew/bin{ShellPathOutputParser.PATH_END_MARKER}";

        string? path = ShellPathOutputParser.ExtractPath(output);

        Assert.Equal("/opt/homebrew/bin", path);
    }

    [Fact]
    public void ExtractPath_MissingStartMarker_ReturnsNull()
    {
        string output = $"/usr/local/bin{ShellPathOutputParser.PATH_END_MARKER}";

        string? path = ShellPathOutputParser.ExtractPath(output);

        Assert.Null(path);
    }

    [Fact]
    public void ExtractPath_MissingEndMarker_ReturnsNull()
    {
        string output = $"{ShellPathOutputParser.PATH_START_MARKER}/usr/local/bin";

        string? path = ShellPathOutputParser.ExtractPath(output);

        Assert.Null(path);
    }

    [Fact]
    public void ExtractPath_EmptyPathBetweenMarkers_ReturnsNull()
    {
        string output = $"{ShellPathOutputParser.PATH_START_MARKER}{ShellPathOutputParser.PATH_END_MARKER}";

        string? path = ShellPathOutputParser.ExtractPath(output);

        Assert.Null(path);
    }

    [Fact]
    public void BuildProbeCommand_ContainsBothMarkers()
    {
        string command = ShellPathOutputParser.BuildProbeCommand();

        Assert.Contains(ShellPathOutputParser.PATH_START_MARKER, command);
        Assert.Contains(ShellPathOutputParser.PATH_END_MARKER, command);
    }
}
