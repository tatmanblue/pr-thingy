using Avalonia;
using PrThingy.App.DependencyInjection;
using PrThingy.Core.DependencyInjection;
using PrThingy.Infrastructure.DependencyInjection;
using PrThingy.Infrastructure.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PrThingy.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services => services
                .AddCore()
                .AddInfrastructure()
                .AddViewModels())
            .Build();

        ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();

        // Last-resort diagnostics: neither hook can stop the process from tearing down once an
        // exception is truly fatal, but without them a crash leaves nothing but a raw console dump.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.LogCritical(e.ExceptionObject as Exception, "Unhandled exception");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        // Must run before host.Start() (which kicks off PrPollingBackgroundService and thus the
        // first `gh` invocation): a macOS app launched via Finder/Dock doesn't inherit the
        // user's shell-profile PATH, so gh/claude/gemini can be unresolvable until this merges
        // in the PATH a login shell would compute. No-op on Windows/Linux.
        host.Services.GetRequiredService<MacOsPathEnvironmentFixer>()
            .ApplyAsync(CancellationToken.None)
            .GetAwaiter().GetResult();

        host.Start();
        try
        {
            BuildAvaloniaApp(host.Services).StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp(IServiceProvider services)
        => AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
