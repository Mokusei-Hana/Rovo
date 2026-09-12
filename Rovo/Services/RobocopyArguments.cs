using Rovo.Models;

namespace Rovo.Services;

public static class RobocopyArguments
{
    public static IReadOnlyList<string> Build(CopyRequest request)
    {
        var arguments = new List<string> { request.Source, request.Destination };
        switch (request.Subfolders)
        {
            case SubfolderMode.None: break;
            case SubfolderMode.NonEmpty: arguments.Add("/S"); break;
            case SubfolderMode.All: arguments.Add("/E"); break;
            default: throw new ArgumentOutOfRangeException(nameof(request));
        }
        arguments.Add($"/R:{request.Retries}");
        arguments.Add($"/W:{request.RetryDelaySeconds}");
        arguments.Add("/XJ");
        arguments.Add("/NP");
        return arguments;
    }
}
