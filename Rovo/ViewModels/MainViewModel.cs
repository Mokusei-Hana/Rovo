using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rovo.Models;
using Rovo.Services;

namespace Rovo.ViewModels;

public partial class MainViewModel(IRobocopyService service) : ObservableObject
{
    private const int OutputLimit = 10_000;
    private readonly object outputLock = new();
    private readonly Queue<string> pendingOutput = new();
    private readonly Queue<string> visibleOutput = new();
    private long droppedLines;
    private CancellationTokenSource? runCancellation;

    [ObservableProperty] private string source = "";
    [ObservableProperty] private string destination = "";
    [ObservableProperty] private SubfolderMode subfolders = SubfolderMode.All;
    [ObservableProperty] private string retries = "2";
    [ObservableProperty] private string retryDelaySeconds = "2";
    [ObservableProperty] private string status = "Choose folders to get started.";
    [ObservableProperty] private string output = "";
    [ObservableProperty] private string? validationError;
    [ObservableProperty] private string outputSummary = "Latest run · up to 10,000 lines";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isCancelling;

    public bool IsIdle => !IsRunning;
    private bool CanStart() => !IsRunning;
    private bool CanCancel() => IsRunning && !IsCancelling;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (IsRunning) return;
        ValidationError = null;
        if (!int.TryParse(Retries, out var retryCount) || retryCount < 0 ||
            !int.TryParse(RetryDelaySeconds, out var delay) || delay < 0)
        {
            ValidationError = "Retries and delay must be nonnegative whole numbers.";
            return;
        }

        var request = new CopyRequest(Source.Trim(), Destination.Trim(), Subfolders, retryCount, delay);
        IsRunning = true;
        IsCancelling = false;
        Status = "Checking folders…";
        using var cancellation = new CancellationTokenSource();
        runCancellation = cancellation;
        try
        {
            // Directory.Exists can block on unavailable UNC paths. Keep the UI responsive,
            // but retain ownership until validation finishes: cancel must never launch a copy later.
            var error = await Task.Run(() => CopyRequestValidator.Validate(request), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (error is not null)
            {
                ValidationError = error;
                Status = "Check the folder paths and try again.";
                return;
            }

            request = request with { Source = Path.GetFullPath(request.Source), Destination = Path.GetFullPath(request.Destination) };
            lock (outputLock)
            {
                pendingOutput.Clear();
                droppedLines = 0;
            }
            visibleOutput.Clear();
            Output = "";
            OutputSummary = "Latest run · up to 10,000 lines";
            Status = "Copying…";
            // Service validation and Process.Start also perform synchronous operating-system work.
            var result = await Task.Run(() => service.RunAsync(request, new OutputProgress(QueueOutput), cancellation.Token), cancellation.Token);
            Status = result.Outcome switch
            {
                CopyOutcome.Completed => result.ExitCode == 0 ? "Completed — no files needed copying." : "Copy completed.",
                CopyOutcome.Attention => "Completed with differences — review the output.",
                CopyOutcome.Canceled => "Canceled — files already copied are kept; an interrupted file may be incomplete.",
                _ => "Copy failed — review the output."
            };
            if (result.ExitCode is { } code)
                Status += $" Exit code: {code}.";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Status = "Canceled.";
        }
        catch (Exception ex)
        {
            Status = $"Copy failed: {ex.Message}";
            QueueOutput(Status);
        }
        finally
        {
            DrainOutput();
            runCancellation = null;
            IsCancelling = false;
            IsRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        if (!CanCancel()) return;
        IsCancelling = true;
        Status = "Canceling — waiting for the current operation to stop…";
        runCancellation?.Cancel();
    }

    private void QueueOutput(string line)
    {
        lock (outputLock)
        {
            pendingOutput.Enqueue(line);
            if (pendingOutput.Count > OutputLimit)
            {
                pendingOutput.Dequeue();
                droppedLines++;
            }
        }
    }

    // Called on the UI thread by the view timer; producer threads never touch bindings.
    public void DrainOutput()
    {
        string[] batch;
        var removed = 0;
        lock (outputLock)
        {
            if (pendingOutput.Count == 0) return;
            // Take one bounded snapshot; continuous producers cannot keep the UI in this loop.
            batch = pendingOutput.ToArray();
            pendingOutput.Clear();
        }
        foreach (var line in batch)
        {
            visibleOutput.Enqueue(line);
            if (visibleOutput.Count > OutputLimit)
            {
                visibleOutput.Dequeue();
                removed++;
            }
        }
        long dropped;
        lock (outputLock)
        {
            droppedLines += removed;
            dropped = droppedLines;
        }
        Output = string.Join(Environment.NewLine, visibleOutput);
        OutputSummary = dropped == 0
            ? $"Latest run · {visibleOutput.Count:N0} lines"
            : $"Latest {visibleOutput.Count:N0} lines · {dropped:N0} older lines omitted";
    }

    private sealed class OutputProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}
