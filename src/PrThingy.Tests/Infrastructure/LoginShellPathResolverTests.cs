using PrThingy.Core.Abstractions;
using PrThingy.Infrastructure.Startup;
using Moq;
using Xunit;

namespace PrThingy.Tests.Infrastructure;

public class LoginShellPathResolverTests
{
    [Fact]
    public async Task ResolveAsync_ProcessRunnerReturnsMarkedOutput_ReturnsExtractedPath()
    {
        string markedOutput = $"{ShellPathOutputParser.PATH_START_MARKER}/opt/homebrew/bin:/usr/local/bin{ShellPathOutputParser.PATH_END_MARKER}";
        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.Arguments.Contains("-lic")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRunResult(0, markedOutput, string.Empty, false));

        LoginShellPathResolver resolver = new LoginShellPathResolver(processRunner.Object);

        string? path = await resolver.ResolveAsync(CancellationToken.None);

        Assert.Equal("/opt/homebrew/bin:/usr/local/bin", path);
    }

    [Fact]
    public async Task ResolveAsync_ProcessRunnerThrows_ReturnsNull()
    {
        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.IsAny<ProcessRunRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("shell not found"));

        LoginShellPathResolver resolver = new LoginShellPathResolver(processRunner.Object);

        string? path = await resolver.ResolveAsync(CancellationToken.None);

        Assert.Null(path);
    }

    [Fact]
    public async Task ResolveAsync_OutputMissingMarkers_ReturnsNull()
    {
        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.IsAny<ProcessRunRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRunResult(0, "some unrelated banner text", string.Empty, false));

        LoginShellPathResolver resolver = new LoginShellPathResolver(processRunner.Object);

        string? path = await resolver.ResolveAsync(CancellationToken.None);

        Assert.Null(path);
    }
}
