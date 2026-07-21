using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;
using PrThingy.Infrastructure.Agents;
using Moq;
using Xunit;

namespace PrThingy.Tests.Infrastructure;

public class CliAgentClientBaseTests
{
    private static Mock<IProcessRunner> ProcessRunnerReturning(out Func<ProcessRunRequest?> capturedRequest)
    {
        ProcessRunRequest? captured = null;
        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(r => r.RunAsync(It.IsAny<ProcessRunRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessRunRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new ProcessRunResult(0, "output", string.Empty, false));
        capturedRequest = () => captured;
        return processRunner;
    }

    // Guards against a real regression: the prompt used to be passed as a command-line argument,
    // which exceeds the Windows command-line length limit once a PR diff is included (surfacing
    // as a misleading Win32Exception "filename or extension is too long"). It must go via stdin.
    [Fact]
    public async Task GenerateBriefingAsync_PassesPromptViaStandardInputNotArguments()
    {
        Mock<IProcessRunner> processRunner = ProcessRunnerReturning(out Func<ProcessRunRequest?> capturedRequest);
        ClaudeCliAgentClient client = new ClaudeCliAgentClient(processRunner.Object);
        string largePrompt = new string('x', 100_000);

        await client.GenerateBriefingAsync(largePrompt, new AgentInvocationOptions(null, AgentEffortLevel.Default), CancellationToken.None);

        ProcessRunRequest? request = capturedRequest();
        Assert.NotNull(request);
        Assert.Equal(largePrompt, request!.StandardInput);
        Assert.DoesNotContain(largePrompt, request.Arguments);
        Assert.All(request.Arguments, arg => Assert.True(arg.Length < 100));
    }

    [Fact]
    public async Task ClaudeCliAgentClient_ModelAndEffortSet_PassesBothFlags()
    {
        Mock<IProcessRunner> processRunner = ProcessRunnerReturning(out Func<ProcessRunRequest?> capturedRequest);
        ClaudeCliAgentClient client = new ClaudeCliAgentClient(processRunner.Object);

        await client.GenerateBriefingAsync("prompt", new AgentInvocationOptions("haiku", AgentEffortLevel.Low), CancellationToken.None);

        IReadOnlyList<string> arguments = capturedRequest()!.Arguments;
        Assert.Equal(["-p", "--model", "haiku", "--effort", "low"], arguments);
    }

    [Fact]
    public async Task ClaudeCliAgentClient_ModelAndEffortUnset_OmitsBothFlags()
    {
        Mock<IProcessRunner> processRunner = ProcessRunnerReturning(out Func<ProcessRunRequest?> capturedRequest);
        ClaudeCliAgentClient client = new ClaudeCliAgentClient(processRunner.Object);

        await client.GenerateBriefingAsync("prompt", new AgentInvocationOptions(null, AgentEffortLevel.Default), CancellationToken.None);

        IReadOnlyList<string> arguments = capturedRequest()!.Arguments;
        Assert.Equal(["-p"], arguments);
    }

    [Fact]
    public async Task GeminiCliAgentClient_ModelSet_PassesModelFlagButNeverEffort()
    {
        Mock<IProcessRunner> processRunner = ProcessRunnerReturning(out Func<ProcessRunRequest?> capturedRequest);
        GeminiCliAgentClient client = new GeminiCliAgentClient(processRunner.Object);

        await client.GenerateBriefingAsync("prompt", new AgentInvocationOptions("gemini-2.5-flash", AgentEffortLevel.Max), CancellationToken.None);

        IReadOnlyList<string> arguments = capturedRequest()!.Arguments;
        Assert.Equal(["-p", "--model", "gemini-2.5-flash"], arguments);
    }

    [Fact]
    public async Task GeminiCliAgentClient_ModelUnset_OmitsModelFlag()
    {
        Mock<IProcessRunner> processRunner = ProcessRunnerReturning(out Func<ProcessRunRequest?> capturedRequest);
        GeminiCliAgentClient client = new GeminiCliAgentClient(processRunner.Object);

        await client.GenerateBriefingAsync("prompt", new AgentInvocationOptions(null, AgentEffortLevel.High), CancellationToken.None);

        IReadOnlyList<string> arguments = capturedRequest()!.Arguments;
        Assert.Equal(["-p"], arguments);
    }
}
