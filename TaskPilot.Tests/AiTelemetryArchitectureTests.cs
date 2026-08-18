using Xunit;

namespace TaskPilot.Tests;

public class AiTelemetryArchitectureTests
{
    [Fact]
    public void DirectChatCalls_AreRestrictedToTelemetryWrapper()
    {
        var root = FindRepositoryRoot();
        var sourceRoots = new[]
        {
            Path.Combine(root, "TaskPilot.AI"),
            Path.Combine(root, "TaskPilot.Services")
        };

        var violations = sourceRoots
            .SelectMany(path => Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.EndsWith("ChatCompletionTelemetryExtensions.cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("GetChatMessageContentAsync(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Direct chat calls bypass telemetry: " + string.Join(", ", violations));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "TaskPilot.sln")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
