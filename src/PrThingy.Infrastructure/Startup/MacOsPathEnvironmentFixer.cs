using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace PrThingy.Infrastructure.Startup;

public sealed class MacOsPathEnvironmentFixer(LoginShellPathResolver resolver, ILogger<MacOsPathEnvironmentFixer> logger)
{
    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        try
        {
            string? resolvedPath = await resolver.ResolveAsync(cancellationToken);
            if (resolvedPath is null)
            {
                logger.LogDebug("Could not resolve login shell PATH; leaving current process PATH unchanged.");
                return;
            }

            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string mergedPath = PathEnvironmentMerger.Merge(resolvedPath, currentPath);
            Environment.SetEnvironmentVariable("PATH", mergedPath);

            logger.LogInformation("Merged login shell PATH into process environment for tool discovery.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply login shell PATH fixup; leaving current process PATH unchanged.");
        }
    }
}
