using System.Diagnostics;
using System.Text;
using PrThingy.Core.Abstractions;

namespace PrThingy.Infrastructure.Processes;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = request.FileName,
                WorkingDirectory = request.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = request.StandardInput is not null,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        foreach (var argument in request.Arguments)
            process.StartInfo.ArgumentList.Add(argument);

        using var timeoutCts = request.Timeout is { } timeout
            ? new CancellationTokenSource(timeout)
            : null;
        using var linkedCts = timeoutCts is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var effectiveToken = linkedCts?.Token ?? cancellationToken;

        process.Start();

        if (request.StandardInput is not null)
        {
            await process.StandardInput.WriteAsync(request.StandardInput);
            process.StandardInput.Close();
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(effectiveToken);
        var stderrTask = process.StandardError.ReadToEndAsync(effectiveToken);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(effectiveToken);
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            if (timeoutCts?.IsCancellationRequested != true)
                throw;

            timedOut = true;
        }

        var standardOutput = await SafeReadAsync(stdoutTask);
        var standardError = await SafeReadAsync(stderrTask);

        return new ProcessRunResult(
            ExitCode: timedOut ? -1 : process.ExitCode,
            StandardOutput: standardOutput,
            StandardError: standardError,
            TimedOut: timedOut);
    }

    private static async Task<string> SafeReadAsync(Task<string> readTask)
    {
        try
        {
            return await readTask;
        }
        catch (OperationCanceledException)
        {
            return string.Empty;
        }
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Process may have already exited between the timeout firing and the kill attempt.
        }
    }
}
