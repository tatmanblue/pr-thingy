using PrThingy.Core.Abstractions;

namespace PrThingy.Infrastructure.Startup;

public sealed class LoginShellPathResolver(IProcessRunner processRunner)
{
    private static readonly TimeSpan SHELL_INVOCATION_TIMEOUT = TimeSpan.FromSeconds(5);
    private const string DEFAULT_SHELL_PATH = "/bin/zsh";

    public async Task<string?> ResolveAsync(CancellationToken cancellationToken)
    {
        string shellPath = Environment.GetEnvironmentVariable("SHELL") is { Length: > 0 } shellFromEnv
            ? shellFromEnv
            : DEFAULT_SHELL_PATH;

        try
        {
            ProcessRunResult result = await processRunner.RunAsync(
                new ProcessRunRequest(
                    shellPath,
                    ["-lic", ShellPathOutputParser.BuildProbeCommand()],
                    Timeout: SHELL_INVOCATION_TIMEOUT),
                cancellationToken);

            return ShellPathOutputParser.ExtractPath(result.StandardOutput);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
