using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Rovo.Models;

namespace Rovo.Services;

public sealed class RobocopyService : IRobocopyService
{
    public async Task<CopyResult> RunAsync(CopyRequest request, IProgress<string> output, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Robocopy requires Windows.");
        var validationError = CopyRequestValidator.Validate(request);
        if (validationError is not null)
            throw new ArgumentException(validationError, nameof(request));
        cancellationToken.ThrowIfCancellationRequested();

        // Robocopy's normal console output uses the Windows OEM code page.
        // Decode it explicitly instead of changing the command to /UNICODE.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding((int)GetOEMCP());
        var startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "robocopy.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = encoding,
            StandardErrorEncoding = encoding
        };
        foreach (var argument in RobocopyArguments.Build(request))
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("Robocopy could not be started.");

        var stdout = DrainAsync(process.StandardOutput, output);
        var stderr = DrainAsync(process.StandardError, output);
        var canceled = false;
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    canceled = true;
                }
                catch (InvalidOperationException) when (process.HasExited) { }
                catch (Win32Exception) when (process.HasExited) { }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // Retain ownership and keep the UI busy until the process exits.
                    output.Report($"Could not stop Robocopy: {ex.Message} Waiting for it to exit.");
                }
            }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        return canceled
            ? new CopyResult(process.ExitCode, CopyOutcome.Canceled)
            : CopyResult.FromExitCode(process.ExitCode);
    }

    private static async Task DrainAsync(StreamReader reader, IProgress<string> output)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            output.Report(line);
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();
}
