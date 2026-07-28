using PrThingy.Infrastructure.Startup;
using Xunit;

namespace PrThingy.Tests.Infrastructure;

public class PathEnvironmentMergerTests
{
    [Fact]
    public void Merge_ShellPathAndCurrentPath_UnionsPreservingShellOrderFirst_Unix()
    {
        if (OperatingSystem.IsWindows())
            return; // Models macOS/Linux PATH convention (':'); see the Windows-flavored sibling below.

        string merged = PathEnvironmentMerger.Merge("/opt/homebrew/bin:/usr/local/bin", "/usr/bin:/bin");

        Assert.Equal("/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin", merged);
    }

    [Fact]
    public void Merge_ShellPathAndCurrentPath_UnionsPreservingShellOrderFirst_Windows()
    {
        if (!OperatingSystem.IsWindows())
            return; // Models Windows PATH convention (';'). Production never hits this OS (MacOsPathEnvironmentFixer
                     // is a macOS-only no-op elsewhere), but the utility method itself is still testable here.

        string merged = PathEnvironmentMerger.Merge(@"C:\opt\bin;C:\tools\bin", @"C:\Windows;C:\Windows\System32");

        Assert.Equal(@"C:\opt\bin;C:\tools\bin;C:\Windows;C:\Windows\System32", merged);
    }

    [Fact]
    public void Merge_DuplicateEntries_Deduplicated_Unix()
    {
        if (OperatingSystem.IsWindows())
            return; // Models macOS/Linux PATH convention (':'); see the Windows-flavored sibling below.

        string merged = PathEnvironmentMerger.Merge("/usr/local/bin:/usr/bin", "/usr/bin:/bin");

        Assert.Equal("/usr/local/bin:/usr/bin:/bin", merged);
    }

    [Fact]
    public void Merge_DuplicateEntries_Deduplicated_Windows()
    {
        if (!OperatingSystem.IsWindows())
            return; // Models Windows PATH convention (';'). Production never hits this OS (MacOsPathEnvironmentFixer
                     // is a macOS-only no-op elsewhere), but the utility method itself is still testable here.

        string merged = PathEnvironmentMerger.Merge(@"C:\tools\bin;C:\Windows", @"C:\Windows;C:\Windows\System32");

        Assert.Equal(@"C:\tools\bin;C:\Windows;C:\Windows\System32", merged);
    }

    [Fact]
    public void Merge_NullOrWhitespaceShellPath_ReturnsCurrentPathUnchanged()
    {
        string merged = PathEnvironmentMerger.Merge(null, "/usr/bin:/bin");

        Assert.Equal("/usr/bin:/bin", merged);
    }

    [Fact]
    public void Merge_NullCurrentPath_ReturnsShellPathEntriesOnly_Unix()
    {
        if (OperatingSystem.IsWindows())
            return; // Models macOS/Linux PATH convention (':'); see the Windows-flavored sibling below.

        string merged = PathEnvironmentMerger.Merge("/opt/homebrew/bin:/usr/local/bin", null);

        Assert.Equal("/opt/homebrew/bin:/usr/local/bin", merged);
    }

    [Fact]
    public void Merge_NullCurrentPath_ReturnsShellPathEntriesOnly_Windows()
    {
        if (!OperatingSystem.IsWindows())
            return; // Models Windows PATH convention (';'). Production never hits this OS (MacOsPathEnvironmentFixer
                     // is a macOS-only no-op elsewhere), but the utility method itself is still testable here.

        string merged = PathEnvironmentMerger.Merge(@"C:\opt\bin;C:\tools\bin", null);

        Assert.Equal(@"C:\opt\bin;C:\tools\bin", merged);
    }
}
