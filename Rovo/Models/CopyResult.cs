namespace Rovo.Models;

public enum CopyOutcome { Completed, Attention, Failed, Canceled }

public sealed record CopyResult(int? ExitCode, CopyOutcome Outcome)
{
    public static CopyResult FromExitCode(int code) => new(code, code switch
    {
        0 or 1 => CopyOutcome.Completed,
        >= 2 and <= 7 => CopyOutcome.Attention,
        _ => CopyOutcome.Failed
    });
}
