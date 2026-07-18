using System.Runtime.InteropServices;

namespace PrThingy.Infrastructure.Storage;

/// <summary>
/// Resolves the per-OS app-data root explicitly rather than relying solely on
/// Environment.SpecialFolder.ApplicationData, which maps to ~/.config on macOS
/// instead of the native ~/Library/Application Support.
/// </summary>
public static class AppPaths
{
    private const string APP_FOLDER_NAME_TITLE_CASE = "PrThingy";
    private const string APP_FOLDER_NAME_LOWER_KEBAB = "pr-thingy";

    public static string RootDirectory { get; } = ResolveRootDirectory();

    public static string SettingsFilePath => Path.Combine(RootDirectory, "settings.json");

    public static string RepositoriesFilePath => Path.Combine(RootDirectory, "repositories.json");

    public static string BriefingsDirectory => Path.Combine(RootDirectory, "briefings");

    public static string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(BriefingsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    private static string ResolveRootDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string appData = Environment.GetEnvironmentVariable("APPDATA")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, APP_FOLDER_NAME_TITLE_CASE);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", APP_FOLDER_NAME_TITLE_CASE);
        }

        string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        string baseDirectory = !string.IsNullOrEmpty(xdgDataHome)
            ? xdgDataHome
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return Path.Combine(baseDirectory, APP_FOLDER_NAME_LOWER_KEBAB);
    }
}
