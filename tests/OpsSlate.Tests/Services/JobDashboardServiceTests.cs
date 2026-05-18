using Microsoft.Extensions.Options;
using OpsSlate.Models;
using OpsSlate.Options;
using OpsSlate.Services;
using Xunit;
using OpsSlate.Tests;

namespace OpsSlate.Tests.Services;

public sealed class JobDashboardServiceTests
{
    [Fact]
    public void GetJobs_MissingDependencyBlocksJob()
    {
        using var temp = TestTempDirectory.Create();
        WriteConfig(temp,
            JobYaml("app", "App", "app/status.json", dependsOn: ["missing"]));
        WriteStatus(temp, "app/status.json", "success");
        var service = CreateService(temp);

        var job = Assert.Single(service.GetJobs());

        Assert.Equal(JobFinalStatus.Blocked, job.FinalStatus);
        Assert.Equal("Blocked by missing", job.Reason);
    }

    [Theory]
    [InlineData("error", JobFinalStatus.Error)]
    [InlineData("success", JobFinalStatus.Stale, null)]
    [InlineData("warning", JobFinalStatus.Warning)]
    [InlineData("unknown", JobFinalStatus.Unknown)]
    public void GetJobs_NonSuccessDependencyStatesBlockJob(string dependencyRawStatus, string expectedDependencyFinalStatus, string? lastRun = "fresh")
    {
        using var temp = TestTempDirectory.Create();
        WriteConfig(temp,
            JobYaml("dependency", "Dependency", "dependency/status.json"),
            JobYaml("app", "App", "app/status.json", dependsOn: ["dependency"]));
        WriteStatus(temp, "dependency/status.json", dependencyRawStatus, lastRun == "fresh" ? DateTimeOffset.UtcNow : DateTimeOffset.UtcNow.AddHours(-2));
        WriteStatus(temp, "app/status.json", "success");
        var service = CreateService(temp);

        var jobs = service.GetJobs();
        var dependency = Assert.Single(jobs, job => job.Id == "dependency");
        var app = Assert.Single(jobs, job => job.Id == "app");

        Assert.Equal(expectedDependencyFinalStatus, dependency.FinalStatus);
        Assert.Equal(JobFinalStatus.Blocked, app.FinalStatus);
        Assert.Equal("Blocked by dependency", app.Reason);
    }

    [Fact]
    public void GetJobs_SuccessDependencyDoesNotBlockJob()
    {
        using var temp = TestTempDirectory.Create();
        WriteConfig(temp,
            JobYaml("dependency", "Dependency", "dependency/status.json"),
            JobYaml("app", "App", "app/status.json", dependsOn: ["dependency"]));
        WriteStatus(temp, "dependency/status.json", "success");
        WriteStatus(temp, "app/status.json", "success");
        var service = CreateService(temp);

        var app = Assert.Single(service.GetJobs(), job => job.Id == "app");

        Assert.Equal(JobFinalStatus.Success, app.FinalStatus);
        Assert.Equal("Job completed successfully", app.Reason);
    }

    [Fact]
    public void GetJobs_CircularDependencyEvaluationTerminatesSafely()
    {
        using var temp = TestTempDirectory.Create();
        WriteConfig(temp,
            JobYaml("first", "First", "first/status.json", dependsOn: ["second"]),
            JobYaml("second", "Second", "second/status.json", dependsOn: ["first"]));
        WriteStatus(temp, "first/status.json", "unknown");
        WriteStatus(temp, "second/status.json", "success");
        var service = CreateService(temp);

        var jobs = service.GetJobs();

        Assert.Equal(2, jobs.Count);
        Assert.All(jobs, job => Assert.Equal(JobFinalStatus.Blocked, job.FinalStatus));
        Assert.Equal("Blocked by second", Assert.Single(jobs, job => job.Id == "first").Reason);
        Assert.Equal("Blocked by first", Assert.Single(jobs, job => job.Id == "second").Reason);
    }

    [Fact]
    public void GetJobs_BlockedOrderingRemainsStableForEqualSortKeys()
    {
        using var temp = TestTempDirectory.Create();
        WriteConfig(temp,
            JobYaml("first", "Same Name", "first/status.json", dependsOn: ["missing"]),
            JobYaml("second", "Same Name", "second/status.json", dependsOn: ["missing"]));
        WriteStatus(temp, "first/status.json", "success");
        WriteStatus(temp, "second/status.json", "success");
        var service = CreateService(temp);

        var jobs = service.GetJobs();

        Assert.Collection(
            jobs,
            first => Assert.Equal("first", first.Id),
            second => Assert.Equal("second", second.Id));
        Assert.All(jobs, job => Assert.Equal(JobFinalStatus.Blocked, job.FinalStatus));
    }

    private static JobDashboardService CreateService(TestTempDirectory temp)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new OpsSlatePathOptions
        {
            ConfigPath = temp.ConfigPath,
            StatusRoot = temp.StatusRoot
        });

        return new JobDashboardService(
            new JobConfigService(options),
            new JobStatusService(),
            new JobHealthEvaluator(new TimeFormatter()),
            options);
    }

    private static void WriteConfig(TestTempDirectory temp, params string[] jobYamlBlocks)
    {
        File.WriteAllText(temp.ConfigPath, "jobs:\n" + string.Concat(jobYamlBlocks));
    }

    private static string JobYaml(string id, string name, string statusPath, IReadOnlyList<string>? dependsOn = null)
    {
        var yaml =
            $"- id: {id}\n" +
            $"  name: {name}\n" +
            $"  status_path: {statusPath}\n" +
            "  stale_after_minutes: 60\n";

        if (dependsOn is null || dependsOn.Count == 0)
        {
            return yaml;
        }

        return yaml + "  depends_on:\n" + string.Concat(dependsOn.Select(dependency => $"  - {dependency}\n"));
    }

    private static void WriteStatus(TestTempDirectory temp, string relativeStatusPath, string status, DateTimeOffset? lastRun = null)
    {
        var fullPath = Path.Combine(temp.StatusRoot, relativeStatusPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath,
            "{\n" +
            $"  \"status\": \"{status}\",\n" +
            $"  \"last_run\": \"{(lastRun ?? DateTimeOffset.UtcNow):O}\",\n" +
            "  \"runtime\": \"00:00:01\",\n" +
            "  \"message\": \"done\",\n" +
            "  \"warnings\": 0,\n" +
            "  \"errors\": 0\n" +
            "}\n");
    }
}
