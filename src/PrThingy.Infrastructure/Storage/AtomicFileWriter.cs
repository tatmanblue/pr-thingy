namespace PrThingy.Infrastructure.Storage;

internal static class AtomicFileWriter
{
    public static async Task WriteAsync(string path, string content, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, content, cancellationToken);
        File.Move(tempPath, path, overwrite: true);
    }
}
