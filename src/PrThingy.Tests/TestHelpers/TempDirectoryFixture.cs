namespace PrThingy.Tests.TestHelpers;

public sealed class TempDirectoryFixture : IDisposable
{
    public string Path { get; } = Directory.CreateDirectory(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pr-thingy-tests-" + Guid.NewGuid())).FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
