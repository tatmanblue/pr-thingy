using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.Infrastructure.Startup;

public sealed class StartupEnvironmentChecker(IProcessRunner processRunner, IAgentClientFactory agentClientFactory)
    : IStartupEnvironmentChecker
{
    private static readonly TimeSpan AVAILABILITY_CHECK_TIMEOUT = TimeSpan.FromSeconds(5);
    private const string GH_CLI_FILE_NAME = "gh";

    public async Task<string?> GetStartupWarningAsync(CancellationToken cancellationToken)
    {
        bool ghAvailable = await IsToolAvailableAsync(GH_CLI_FILE_NAME, cancellationToken);
        bool ghAuthenticated = ghAvailable && await IsGhAuthenticatedAsync(cancellationToken);

        List<string> agentCliNames = [];
        bool anyAgentAvailable = false;
        foreach (AgentType agentType in Enum.GetValues<AgentType>())
        {
            string cliFileName = agentClientFactory.GetClient(agentType).CliFileName;
            agentCliNames.Add(cliFileName);
            if (await IsToolAvailableAsync(cliFileName, cancellationToken))
                anyAgentAvailable = true;
        }

        List<string> missing = [];
        if (!ghAvailable)
            missing.Add("GitHub CLI (gh)");
        else if (!ghAuthenticated)
            missing.Add("GitHub CLI authentication (run 'gh auth login')");
        if (!anyAgentAvailable)
            missing.Add($"an agent CLI ({string.Join(" or ", agentCliNames)})");

        if (missing.Count == 0)
            return null;

        string requiredTools = missing.Count == 1 ? "required tool" : "required tools";
        return $"Missing {requiredTools}: {string.Join(" and ", missing)}. See the README for setup details.";
    }

    private async Task<bool> IsToolAvailableAsync(string fileName, CancellationToken cancellationToken)
    {
        try
        {
            await processRunner.RunAsync(
                new ProcessRunRequest(fileName, ["--version"], Timeout: AVAILABILITY_CHECK_TIMEOUT),
                cancellationToken);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<bool> IsGhAuthenticatedAsync(CancellationToken cancellationToken)
    {
        try
        {
            ProcessRunResult result = await processRunner.RunAsync(
                new ProcessRunRequest(GH_CLI_FILE_NAME, ["auth", "status"], Timeout: AVAILABILITY_CHECK_TIMEOUT),
                cancellationToken);
            return result.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
