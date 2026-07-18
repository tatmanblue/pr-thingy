using PrThingy.Core.Abstractions;
using PrThingy.Infrastructure.Processes;
using Xunit;

namespace PrThingy.Tests.Infrastructure;

public class ProcessRunnerTests
{
    private readonly ProcessRunner runner = new();

    [Fact]
    public async Task RunAsync_HappyPath_CapturesExitCodeAndStdout()
    {
        var result = await runner.RunAsync(
            new ProcessRunRequest("dotnet", ["--version"]),
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.False(string.IsNullOrWhiteSpace(result.StandardOutput));
    }

    [Fact]
    public async Task RunAsync_NonZeroExitCode_CapturesStandardError()
    {
        var result = await runner.RunAsync(
            new ProcessRunRequest("dotnet", ["not-a-real-command"]),
            CancellationToken.None);

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_StandardInput_IsPassedToProcess()
    {
        var result = await runner.RunAsync(
            new ProcessRunRequest("cat", [], StandardInput: "hello from stdin"),
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("hello from stdin", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_AlreadyCancelledToken_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(new ProcessRunRequest("dotnet", ["--version"]), cts.Token));
    }

    [Fact]
    public async Task RunAsync_TimeoutExceeded_ReturnsTimedOutResult()
    {
        var result = await runner.RunAsync(
            new ProcessRunRequest("sleep", ["5"], Timeout: TimeSpan.FromMilliseconds(200)),
            CancellationToken.None);

        Assert.True(result.TimedOut);
    }
}
