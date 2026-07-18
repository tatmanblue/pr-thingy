using System.Text.Json;
using PrThingy.Core.Abstractions;
using PrThingy.Core.Models;

namespace PrThingy.Infrastructure.Storage;

public sealed class FileAppSettingsStore(string settingsFilePath) : IAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(settingsFilePath))
            return new AppSettings();

        await fileLock.WaitAsync(cancellationToken);
        try
        {
            string json = await File.ReadAllTextAsync(settingsFilePath, cancellationToken);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(settings, JsonOptions);

        await fileLock.WaitAsync(cancellationToken);
        try
        {
            await AtomicFileWriter.WriteAsync(settingsFilePath, json, cancellationToken);
        }
        finally
        {
            fileLock.Release();
        }
    }
}
