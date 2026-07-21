using PrThingy.Infrastructure.Startup;
using Xunit;

namespace PrThingy.Tests.Infrastructure;

public class PathEnvironmentMergerTests
{
    [Fact]
    public void Merge_ShellPathAndCurrentPath_UnionsPreservingShellOrderFirst()
    {
        string merged = PathEnvironmentMerger.Merge("/opt/homebrew/bin:/usr/local/bin", "/usr/bin:/bin");

        Assert.Equal(string.Join(Path.PathSeparator, "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin", "/bin"), merged);
    }

    [Fact]
    public void Merge_DuplicateEntries_Deduplicated()
    {
        string merged = PathEnvironmentMerger.Merge("/usr/local/bin:/usr/bin", "/usr/bin:/bin");

        Assert.Equal(string.Join(Path.PathSeparator, "/usr/local/bin", "/usr/bin", "/bin"), merged);
    }

    [Fact]
    public void Merge_NullOrWhitespaceShellPath_ReturnsCurrentPathUnchanged()
    {
        string merged = PathEnvironmentMerger.Merge(null, "/usr/bin:/bin");

        Assert.Equal("/usr/bin:/bin", merged);
    }

    [Fact]
    public void Merge_NullCurrentPath_ReturnsShellPathEntriesOnly()
    {
        string merged = PathEnvironmentMerger.Merge("/opt/homebrew/bin:/usr/local/bin", null);

        Assert.Equal(string.Join(Path.PathSeparator, "/opt/homebrew/bin", "/usr/local/bin"), merged);
    }
}
