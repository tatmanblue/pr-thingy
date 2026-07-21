using System.Runtime.InteropServices;
using PrThingy.Core.Abstractions;
using PrThingy.Infrastructure.Startup;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace PrThingy.Tests.Infrastructure;

public class MacOsPathEnvironmentFixerTests
{
    [Fact]
    public async Task ApplyAsync_ResolverReturnsNull_DoesNotMutatePath()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.IsAny<ProcessRunRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRunResult(0, "no markers here", string.Empty, false));

        LoginShellPathResolver resolver = new LoginShellPathResolver(processRunner.Object);
        MacOsPathEnvironmentFixer fixer = new MacOsPathEnvironmentFixer(resolver, NullLogger<MacOsPathEnvironmentFixer>.Instance);

        string? pathBefore = Environment.GetEnvironmentVariable("PATH");
        await fixer.ApplyAsync(CancellationToken.None);
        string? pathAfter = Environment.GetEnvironmentVariable("PATH");

        Assert.Equal(pathBefore, pathAfter);
    }

    [Fact]
    public async Task ApplyAsync_ResolverThrows_DoesNotThrowAndDoesNotMutatePath()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.IsAny<ProcessRunRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        LoginShellPathResolver resolver = new LoginShellPathResolver(processRunner.Object);
        MacOsPathEnvironmentFixer fixer = new MacOsPathEnvironmentFixer(resolver, NullLogger<MacOsPathEnvironmentFixer>.Instance);

        string? pathBefore = Environment.GetEnvironmentVariable("PATH");
        await fixer.ApplyAsync(CancellationToken.None);
        string? pathAfter = Environment.GetEnvironmentVariable("PATH");

        Assert.Equal(pathBefore, pathAfter);
    }

    [Fact]
    public async Task ApplyAsync_NotMacOS_DoesNotInvokeResolver()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.IsAny<ProcessRunRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRunResult(0, "should never be reached", string.Empty, false));

        LoginShellPathResolver resolver = new LoginShellPathResolver(processRunner.Object);
        MacOsPathEnvironmentFixer fixer = new MacOsPathEnvironmentFixer(resolver, NullLogger<MacOsPathEnvironmentFixer>.Instance);

        await fixer.ApplyAsync(CancellationToken.None);

        processRunner.Verify(p => p.RunAsync(It.IsAny<ProcessRunRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
