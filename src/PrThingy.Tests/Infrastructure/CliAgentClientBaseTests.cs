using PrThingy.Core.Abstractions;
using PrThingy.Infrastructure.Agents;
using Moq;
using Xunit;

namespace PrThingy.Tests.Infrastructure;

public class CliAgentClientBaseTests
{
    // Guards against a real regression: the prompt used to be passed as a command-line argument,
    // which exceeds the Windows command-line length limit once a PR diff is included (surfacing
    // as a misleading Win32Exception "filename or extension is too long"). It must go via stdin.
    [Fact]
    public async Task GenerateBriefingAsync_PassesPromptViaStandardInputNotArguments()
    {
        ProcessRunRequest? capturedRequest = null;
        Mock<IProcessRunner> processRunner = new Mock<IProcessRunner>();
        processRunner
            .Setup(r => r.RunAsync(It.IsAny<ProcessRunRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessRunRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new ProcessRunResult(0, "output", string.Empty, false));

        ClaudeCliAgentClient client = new ClaudeCliAgentClient(processRunner.Object);
        string largePrompt = new string('x', 100_000);

        await client.GenerateBriefingAsync(largePrompt, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(largePrompt, capturedRequest!.StandardInput);
        Assert.DoesNotContain(largePrompt, capturedRequest.Arguments);
        Assert.All(capturedRequest.Arguments, arg => Assert.True(arg.Length < 100));
    }
}
