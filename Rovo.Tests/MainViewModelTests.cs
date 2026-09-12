using Rovo.Models;
using Rovo.Services;
using Rovo.ViewModels;

namespace Rovo.Tests;

public sealed class MainViewModelTests
{
    [Theory]
    [InlineData(0, "no files needed")]
    [InlineData(1, "Copy completed")]
    [InlineData(3, "differences")]
    [InlineData(8, "failed")]
    public async Task ShowsOutcomeAndDrainsFinalOutput(int exitCode, string status)
    {
        using var folders = new TestFolders();
        var service = new FakeService((_, output, _) =>
        {
            output.Report("last output line");
            return Task.FromResult(CopyResult.FromExitCode(exitCode));
        });
        var vm = Create(service, folders);
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Contains(status, vm.Status);
        Assert.Contains($"Exit code: {exitCode}", vm.Status);
        Assert.Equal("last output line", vm.Output);
        Assert.True(vm.IsIdle);
        Assert.True(vm.StartCommand.CanExecute(null));
        Assert.False(vm.CancelCommand.CanExecute(null));
    }

    [Fact]
    public async Task CancelsAndCanStartAnotherRun()
    {
        using var folders = new TestFolders();
        var calls = 0;
        var service = new FakeService(async (_, _, token) =>
        {
            calls++;
            if (calls == 1) await Task.Delay(Timeout.Infinite, token);
            return CopyResult.FromExitCode(1);
        });
        var vm = Create(service, folders);
        var running = vm.StartCommand.ExecuteAsync(null);
        Assert.True(vm.IsRunning);
        Assert.False(vm.StartCommand.CanExecute(null));
        // Even a direct programmatic duplicate invocation must not start a second process.
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(1, calls);
        vm.CancelCommand.Execute(null);
        await running;
        Assert.Equal("Canceled.", vm.Status);
        Assert.False(vm.IsRunning);
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(2, calls);
        Assert.Contains("Copy completed", vm.Status);
    }

    [Fact]
    public async Task ReportsServiceFailureAndRestoresControls()
    {
        using var folders = new TestFolders();
        var vm = Create(new FakeService((_, _, _) => throw new InvalidOperationException("Cannot launch Robocopy")), folders);
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Contains("Cannot launch Robocopy", vm.Status);
        Assert.Equal(vm.Status, vm.Output);
        Assert.True(vm.IsIdle);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("9999999999999")]
    public async Task InvalidRetriesNeverInvokeService(string retries)
    {
        using var folders = new TestFolders();
        var vm = Create(new FakeService((_, _, _) => throw new Xunit.Sdk.XunitException("Should not run")), folders);
        vm.Retries = retries;
        await vm.StartCommand.ExecuteAsync(null);
        Assert.NotNull(vm.ValidationError);
        Assert.True(vm.IsIdle);
        Assert.DoesNotContain("Should not run", vm.Output);
    }

    [Fact]
    public async Task BoundsOutputAndClearsItForNextRun()
    {
        using var folders = new TestFolders();
        var calls = 0;
        var vm = Create(new FakeService((_, output, _) =>
        {
            if (++calls == 1)
                for (var i = 0; i < 15_000; i++) output.Report($"line {i}");
            else output.Report("next run");
            return Task.FromResult(CopyResult.FromExitCode(1));
        }), folders);
        await vm.StartCommand.ExecuteAsync(null);
        var lines = vm.Output.Split(Environment.NewLine);
        Assert.Equal(10_000, lines.Length);
        Assert.Equal("line 5000", lines[0]);
        Assert.Equal("line 14999", lines[^1]);
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Equal("next run", vm.Output);
    }

    private static MainViewModel Create(IRobocopyService service, TestFolders folders) => new(service)
    {
        Source = folders.Source,
        Destination = folders.Destination
    };

    private sealed class FakeService(Func<CopyRequest, IProgress<string>, CancellationToken, Task<CopyResult>> run) : IRobocopyService
    {
        public Task<CopyResult> RunAsync(CopyRequest request, IProgress<string> output, CancellationToken token) => run(request, output, token);
    }
}
