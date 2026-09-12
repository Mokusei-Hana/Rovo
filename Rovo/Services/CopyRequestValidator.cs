using System.IO;
using Rovo.Models;

namespace Rovo.Services;

public static class CopyRequestValidator
{
    public static string? Validate(CopyRequest request)
    {
        if (!Enum.IsDefined(request.Subfolders))
            return "Choose a subfolder option.";
        if (request.Retries < 0 || request.RetryDelaySeconds < 0)
            return "Retries and delay must be nonnegative whole numbers.";

        try
        {
            if (!IsOrdinaryAbsolutePath(request.Source) || !IsOrdinaryAbsolutePath(request.Destination))
                return "Enter absolute source and destination folder paths (local or UNC).";

            var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.Source));
            var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.Destination));
            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) ||
                IsWithin(source, destination) || IsWithin(destination, source))
                return "For safety, Rovo requires separate folders: neither folder may contain the other.";

            if (!Directory.Exists(source))
                return "The source folder does not exist or cannot be accessed.";
            if (File.Exists(destination))
                return "The destination is a file. Choose a folder path.";
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return "One of the folder paths is invalid or cannot be accessed.";
        }

        return null;
    }

    private static bool IsOrdinaryAbsolutePath(string path) =>
        !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) &&
        path.IndexOfAny(Path.GetInvalidPathChars()) < 0 &&
        path.IndexOfAny(['*', '?', '"']) < 0 &&
        !path.StartsWith(@"\\.\", StringComparison.Ordinal) &&
        !path.StartsWith(@"\\?\", StringComparison.Ordinal);

    private static bool IsWithin(string candidate, string parent)
    {
        var prefix = Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
