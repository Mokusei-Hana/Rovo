namespace Rovo.Models;

public enum SubfolderMode { None, NonEmpty, All }

public sealed record CopyRequest(
    string Source,
    string Destination,
    SubfolderMode Subfolders = SubfolderMode.All,
    int Retries = 2,
    int RetryDelaySeconds = 2);
