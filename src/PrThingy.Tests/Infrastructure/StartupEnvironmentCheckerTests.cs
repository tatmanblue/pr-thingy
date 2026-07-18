using System.ComponentModel;
using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;
using PrThingy.Infrastructure.Startup;
using PrThingy.Tests.TestHelpers;
using Moq;
using Xunit;

namespace PrThingy.Tests.Infrastructure;

public class StartupEnvironmentCheckerTests
{
    private static Mock<IAgentClientFactory> CreateAgentClientFactory()
    {
        Mock<IAgentClientFactory> factory = new Mock<IAgentClientFactory>();
        factory.Setup(f => f.GetClient(AgentType.Claude))
            .Returns(new FakeAgentClient(AgentType.Claude, _ => new AgentInvocationResult(true, string.Empty, null, TimeSpan.Zero)));
        factory.Setup(f => f.GetClient(AgentType.Gemini))
            .Returns(new FakeAgentClient(AgentType.Gemini, _ => new AgentInvocationResult(true, string.Empty, null, TimeSpan.Zero)));
        return factory;
    }

    private static ProcessRunResult Success() => new ProcessRunResult(0, "version 1.0", string.Empty, false);

    private static ProcessRunResult Failure() => new ProcessRunResult(1, string.Empty, "error", false);

    [Fact]
    public async Task GetStartupWarningAsync_AllToolsPresent_ReturnsNull()
    {
        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.IsAny<ProcessRunRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success());

        StartupEnvironmentChecker checker = new StartupEnvironmentChecker(processRunner.Object, CreateAgentClientFactory().Object);

        string? warning = await checker.GetStartupWarningAsync(CancellationToken.None);

        Assert.Null(warning);
    }

    [Fact]
    public async Task GetStartupWarningAsync_GhPresentButNotAuthenticated_NamesAuthProblem()
    {
        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName == "gh" && r.Arguments.SequenceEqual(new[] { "--version" })), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success());
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName == "gh" && r.Arguments.SequenceEqual(new[] { "auth", "status" })), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Failure());
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName != "gh"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success());

        StartupEnvironmentChecker checker = new StartupEnvironmentChecker(processRunner.Object, CreateAgentClientFactory().Object);

        string? warning = await checker.GetStartupWarningAsync(CancellationToken.None);

        Assert.NotNull(warning);
        Assert.Contains("GitHub CLI authentication", warning);
        Assert.Contains("gh auth login", warning);
        Assert.DoesNotContain("GitHub CLI (gh)", warning);
    }

    [Fact]
    public async Task GetStartupWarningAsync_GhMissing_DoesNotAlsoReportAuthProblem()
    {
        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName == "gh"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Win32Exception("not found"));
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName != "gh"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success());

        StartupEnvironmentChecker checker = new StartupEnvironmentChecker(processRunner.Object, CreateAgentClientFactory().Object);

        string? warning = await checker.GetStartupWarningAsync(CancellationToken.None);

        Assert.NotNull(warning);
        Assert.Contains("GitHub CLI (gh)", warning);
        Assert.DoesNotContain("GitHub CLI authentication", warning);
    }

    [Fact]
    public async Task GetStartupWarningAsync_GhMissing_NamesGhOnly()
    {
        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName == "gh"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Win32Exception("not found"));
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName != "gh"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success());

        StartupEnvironmentChecker checker = new StartupEnvironmentChecker(processRunner.Object, CreateAgentClientFactory().Object);

        string? warning = await checker.GetStartupWarningAsync(CancellationToken.None);

        Assert.NotNull(warning);
        Assert.Contains("GitHub CLI (gh)", warning);
        Assert.DoesNotContain("agent CLI", warning);
        Assert.Contains("README", warning);
    }

    [Fact]
    public async Task GetStartupWarningAsync_AllAgentClisMissing_NamesAgentCliOnly()
    {
        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName == "gh"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success());
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName == "claude" || r.FileName == "gemini"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Win32Exception("not found"));

        StartupEnvironmentChecker checker = new StartupEnvironmentChecker(processRunner.Object, CreateAgentClientFactory().Object);

        string? warning = await checker.GetStartupWarningAsync(CancellationToken.None);

        Assert.NotNull(warning);
        Assert.DoesNotContain("GitHub CLI (gh)", warning);
        Assert.Contains("an agent CLI (claude or gemini)", warning);
    }

    [Fact]
    public async Task GetStartupWarningAsync_OneAgentCliAvailable_ReturnsNull()
    {
        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName == "claude"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Win32Exception("not found"));
        processRunner
            .Setup(p => p.RunAsync(It.Is<ProcessRunRequest>(r => r.FileName != "claude"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success());

        StartupEnvironmentChecker checker = new StartupEnvironmentChecker(processRunner.Object, CreateAgentClientFactory().Object);

        string? warning = await checker.GetStartupWarningAsync(CancellationToken.None);

        Assert.Null(warning);
    }

    [Fact]
    public async Task GetStartupWarningAsync_AllToolsMissing_NamesBoth()
    {
        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(p => p.RunAsync(It.IsAny<ProcessRunRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Win32Exception("not found"));

        StartupEnvironmentChecker checker = new StartupEnvironmentChecker(processRunner.Object, CreateAgentClientFactory().Object);

        string? warning = await checker.GetStartupWarningAsync(CancellationToken.None);

        Assert.NotNull(warning);
        Assert.Contains("GitHub CLI (gh)", warning);
        Assert.Contains("an agent CLI (claude or gemini)", warning);
        Assert.Contains("required tools", warning);
    }
}
