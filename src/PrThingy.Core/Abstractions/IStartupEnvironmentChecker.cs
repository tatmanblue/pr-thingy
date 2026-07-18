namespace PrThingy.Core.Abstractions;

public interface IStartupEnvironmentChecker
{
    Task<string?> GetStartupWarningAsync(CancellationToken cancellationToken);
}
