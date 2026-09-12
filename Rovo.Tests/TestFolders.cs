namespace Rovo.Tests;

public sealed class TestFolders : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "Rovo.Tests", Guid.NewGuid().ToString("N"));
    public string Source => Path.Combine(Root, "source");
    public string Destination => Path.Combine(Root, "destination");

    public TestFolders() => Directory.CreateDirectory(Source);

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
