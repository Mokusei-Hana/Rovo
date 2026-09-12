using Rovo.Models;
using Rovo.Services;

namespace Rovo.Tests;

public sealed class CopyBehaviorTests
{
    [Theory]
    [InlineData(SubfolderMode.None, null)]
    [InlineData(SubfolderMode.NonEmpty, "/S")]
    [InlineData(SubfolderMode.All, "/E")]
    public void ArgumentsPreservePathsAndOnlyEnableSupportedOptions(SubfolderMode mode, string? recursion)
    {
        var request = new CopyRequest(@"C:\source & files\", @"D:\目标 folder\", mode, 0, 3);
        var expected = new List<string> { request.Source, request.Destination };
        if (recursion is not null) expected.Add(recursion);
        expected.AddRange(["/R:0", "/W:3", "/XJ", "/NP"]);
        Assert.Equal(expected, RobocopyArguments.Build(request));
    }

    [Theory]
    [InlineData(0, CopyOutcome.Completed)]
    [InlineData(1, CopyOutcome.Completed)]
    [InlineData(2, CopyOutcome.Attention)]
    [InlineData(3, CopyOutcome.Attention)]
    [InlineData(4, CopyOutcome.Attention)]
    [InlineData(5, CopyOutcome.Attention)]
    [InlineData(6, CopyOutcome.Attention)]
    [InlineData(7, CopyOutcome.Attention)]
    [InlineData(8, CopyOutcome.Failed)]
    [InlineData(16, CopyOutcome.Failed)]
    [InlineData(-1, CopyOutcome.Failed)]
    public void ClassifiesRobocopyExitCodes(int code, CopyOutcome expected)
    {
        var result = CopyResult.FromExitCode(code);
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(code, result.ExitCode);
    }

    [Fact]
    public void AcceptsMissingDestinationAndSiblingWithSamePrefix()
    {
        using var folders = new TestFolders();
        Assert.Null(CopyRequestValidator.Validate(new(folders.Source, folders.Destination)));
        Assert.Null(CopyRequestValidator.Validate(new(folders.Source, folders.Source + "-copy")));
    }

    [Fact]
    public void RejectsOverlappingPathsAsRovoPolicy()
    {
        using var folders = new TestFolders();
        foreach (var destination in new[] { folders.Source, folders.Source.ToUpperInvariant(), Path.Combine(folders.Source, "child"), folders.Root,
                     Path.Combine(folders.Source, "child", "..") })
        {
            var error = CopyRequestValidator.Validate(new(folders.Source, destination));
            Assert.Contains("For safety, Rovo", error);
        }
    }

    [Fact]
    public void RejectsMissingSourceAndDestinationFile()
    {
        using var folders = new TestFolders();
        Assert.Contains("source folder", CopyRequestValidator.Validate(new(folders.Destination, folders.Source)));
        File.WriteAllText(folders.Destination, "existing file");
        Assert.Contains("destination is a file", CopyRequestValidator.Validate(new(folders.Source, folders.Destination)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative/path")]
    [InlineData("/path/with\0null")]
    public void RejectsInvalidPaths(string source)
    {
        using var folders = new TestFolders();
        Assert.NotNull(CopyRequestValidator.Validate(new(source, folders.Destination)));
    }

    [Fact]
    public void RejectsInvalidOptions()
    {
        using var folders = new TestFolders();
        Assert.NotNull(CopyRequestValidator.Validate(new(folders.Source, folders.Destination, Retries: -1)));
        Assert.NotNull(CopyRequestValidator.Validate(new(folders.Source, folders.Destination, RetryDelaySeconds: -1)));
        Assert.NotNull(CopyRequestValidator.Validate(new(folders.Source, folders.Destination, (SubfolderMode)99)));
    }
}
