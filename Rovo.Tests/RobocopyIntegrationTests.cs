using Rovo.Models;
using Rovo.Services;

namespace Rovo.Tests;

public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires Windows and the real Robocopy executable.";
    }
}

public sealed class RobocopyIntegrationTests
{
    [WindowsFact]
    public async Task CopiesUnicodePathsUpdatesFilesAndPreservesDestinationOnlyFiles()
    {
        using var folders = new TestFolders();
        var source = Directory.CreateDirectory(Path.Combine(folders.Source, "资料 & files")).FullName;
        Directory.CreateDirectory(Path.Combine(source, "empty"));
        Directory.CreateDirectory(folders.Destination);
        File.WriteAllText(Path.Combine(source, "hello.txt"), "new content with a different size");
        File.WriteAllText(Path.Combine(folders.Destination, "hello.txt"), "old");
        File.WriteAllText(Path.Combine(folders.Destination, "keep.txt"), "keep");
        var output = new CollectedOutput();
        var result = await new RobocopyService().RunAsync(new(source, folders.Destination), output, CancellationToken.None);
        Assert.True(result.ExitCode is >= 0 and < 8);
        Assert.Equal("new content with a different size", File.ReadAllText(Path.Combine(folders.Destination, "hello.txt")));
        Assert.True(File.Exists(Path.Combine(folders.Destination, "keep.txt")));
        Assert.True(Directory.Exists(Path.Combine(folders.Destination, "empty")));
        Assert.NotEmpty(output.Lines);
    }

    [WindowsFact]
    public async Task SubfolderModesControlEmptyAndPopulatedDirectories()
    {
        using var folders = new TestFolders();
        Directory.CreateDirectory(Path.Combine(folders.Source, "empty"));
        var populated = Directory.CreateDirectory(Path.Combine(folders.Source, "populated")).FullName;
        File.WriteAllText(Path.Combine(populated, "child.txt"), "child");
        File.WriteAllText(Path.Combine(folders.Source, "root.txt"), "root");
        foreach (var mode in Enum.GetValues<SubfolderMode>())
        {
            var destination = folders.Destination + mode;
            var result = await new RobocopyService().RunAsync(new(folders.Source, destination, mode), new CollectedOutput(), CancellationToken.None);
            Assert.True(result.ExitCode is >= 0 and < 8);
            Assert.True(File.Exists(Path.Combine(destination, "root.txt")));
            Assert.Equal(mode != SubfolderMode.None, File.Exists(Path.Combine(destination, "populated", "child.txt")));
            Assert.Equal(mode == SubfolderMode.All, Directory.Exists(Path.Combine(destination, "empty")));
        }
    }

    [WindowsFact]
    public async Task LockedFileWithNoRetriesReportsFailureThenRecovers()
    {
        using var folders = new TestFolders();
        var lockedPath = Path.Combine(folders.Source, "locked.txt");
        File.WriteAllText(lockedPath, "locked content");
        var service = new RobocopyService();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using (var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var output = new CollectedOutput();
            var result = await service.RunAsync(new(folders.Source, folders.Destination, Retries: 0, RetryDelaySeconds: 0),
                output, timeout.Token);
            Assert.Equal(CopyOutcome.Failed, result.Outcome);
            Assert.True(result.ExitCode >= 8);
            Assert.NotEmpty(output.Lines);
        }
        var next = await service.RunAsync(new(folders.Source, folders.Destination), new CollectedOutput(), timeout.Token);
        Assert.True(next.ExitCode is >= 0 and < 8);
        Assert.Equal("locked content", File.ReadAllText(Path.Combine(folders.Destination, "locked.txt")));
    }

    [WindowsFact]
    public async Task PreCanceledRequestDoesNotCreateDestination()
    {
        using var folders = new TestFolders();
        File.WriteAllText(Path.Combine(folders.Source, "file.txt"), "content");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var output = new CollectedOutput();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RobocopyService().RunAsync(
            new(folders.Source, folders.Destination), output, cancellation.Token));
        Assert.False(Directory.Exists(folders.Destination));
        Assert.Empty(output.Lines);
    }

    [WindowsFact]
    public async Task CanCancelDuringRetryThenRunAgain()
    {
        using var folders = new TestFolders();
        var lockedPath = Path.Combine(folders.Source, "locked.txt");
        File.WriteAllText(lockedPath, "locked content");
        var service = new RobocopyService();
        using (var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
        {
            var result = await service.RunAsync(new(folders.Source, folders.Destination, Retries: 100, RetryDelaySeconds: 2),
                new CollectedOutput(), cancellation.Token).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(CopyOutcome.Canceled, result.Outcome);
        }
        var next = await service.RunAsync(new(folders.Source, folders.Destination), new CollectedOutput(), CancellationToken.None);
        Assert.True(next.ExitCode is >= 0 and < 8);
        Assert.Equal("locked content", File.ReadAllText(Path.Combine(folders.Destination, "locked.txt")));
    }

    private sealed class CollectedOutput : IProgress<string>
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Lines { get; } = new();
        public void Report(string value) => Lines.Enqueue(value);
    }
}
