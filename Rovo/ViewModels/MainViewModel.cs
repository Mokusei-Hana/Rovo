using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rovo.Models;
using Rovo.Services;

namespace Rovo.ViewModels;

public partial class MainViewModel(IRobocopyService service) : ObservableObject
{
    private const int OutputLimit = 10_000;
    private readonly ConcurrentQueue<string> pendingOutput = new();
    private readonly Queue<string> visibleOutput = new();
    private CancellationTokenSource? runCancellation;

    [ObservableProperty] private string source = "";
    [ObservableProperty] private string destination = "";
    [ObservableProperty] private SubfolderMode subfolders = SubfolderMode.All;
    [ObservableProperty] private string retries = "2";
    [ObservableProperty] private string retryDelaySeconds = "2";
    [ObservableProperty] private string status = "Choose folders to get started.";
    [ObservableProperty] private string output = "";
    [ObservableProperty] private string? validationError;

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
        ValidationError = CopyRequestValidator.Validate(request);
        if (ValidationError is not null) return;

        // Snapshot normalized paths and options before starting background work.
        request = request with { Source = Path.GetFullPath(request.Source), Destination = Path.GetFullPath(request.Destination) };
        pendingOutput.Clear();
        visibleOutput.Clear();
        Output = "";
        IsRunning = true;
        IsCancelling = false;
        Status = "Copying…";
        using var cancellation = new CancellationTokenSource();
        runCancellation = cancellation;
        try
        {
            var result = await service.RunAsync(request, new OutputProgress(QueueOutput), cancellation.Token);
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
        Status = "Canceling — waiting for Robocopy to stop…";
        runCancellation?.Cancel();
    }

    private void QueueOutput(string line)
    {
        pendingOutput.Enqueue(line);
        while (pendingOutput.Count > OutputLimit)
            pendingOutput.TryDequeue(out _);
    }

    // Called on the UI thread by the view timer; producer threads never touch bindings.
    public void DrainOutput()
    {
        var changed = false;
        while (pendingOutput.TryDequeue(out var line))
        {
            visibleOutput.Enqueue(line);
            if (visibleOutput.Count > OutputLimit) visibleOutput.Dequeue();
            changed = true;
        }
        if (changed) Output = string.Join(Environment.NewLine, visibleOutput);
    }

    private sealed class OutputProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}
