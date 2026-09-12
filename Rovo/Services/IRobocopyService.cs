using Rovo.Models;

namespace Rovo.Services;

public interface IRobocopyService
{
    Task<CopyResult> RunAsync(CopyRequest request, IProgress<string> output, CancellationToken cancellationToken);
}
