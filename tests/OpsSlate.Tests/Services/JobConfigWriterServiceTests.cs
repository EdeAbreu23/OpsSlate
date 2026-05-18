using Microsoft.Extensions.Options;
using OpsSlate.Models;
using OpsSlate.Options;
using OpsSlate.Services;
using Xunit;
using OpsSlate.Tests;

namespace OpsSlate.Tests.Services;

public sealed class JobConfigWriterServiceTests
{
    [Fact]
    public void AddJob_WritesExpectedYaml()
    {
        using var temp = TestTempDirectory.Create();
        var service = CreateService(temp.ConfigPath, temp.StatusRoot);

        var result = service.AddJob(new JobConfig
        {
            Id = "backup",
            Name = "Nightly Backup",
            StatusPath = "backup/status.json",
            StaleAfterMinutes = 45,
            DependsOn = ["database", "storage"]
        });

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(
            "jobs:\n" +
            "- id: backup\n" +
            "  name: Nightly Backup\n" +
            "  status_path: backup/status.json\n" +
            "  stale_after_minutes: 45\n" +
            "  depends_on:\n" +
            "  - database\n" +
            "  - storage\n",
            Normalize(File.ReadAllText(temp.ConfigPath)));
    }

    [Fact]
    public void AddJob_CreatesTimestampBackupBeforeWriteWhenConfigExists()
    {
        using var temp = TestTempDirectory.Create();
        var originalYaml =
            "jobs:\n" +
            "- id: existing\n" +
            "  name: Existing\n" +
            "  status_path: existing/status.json\n" +
            "  stale_after_minutes: 60\n";
        File.WriteAllText(temp.ConfigPath, originalYaml);
        var service = CreateService(temp.ConfigPath, temp.StatusRoot);

        var result = service.AddJob(Job("new-job", "New Job", "new/status.json"));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(result.BackupPath);
        Assert.Matches(@"jobs\.yml\.bak\.\d{17}$", result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));
        Assert.Equal(originalYaml, File.ReadAllText(result.BackupPath));
        Assert.Contains("id: new-job", File.ReadAllText(temp.ConfigPath));
    }

    [Fact]
    public void AddJob_CleansUpTempFileAfterWriteFailureWhenPossible()
    {
        using var temp = TestTempDirectory.Create();
        var configDirectoryPath = Path.Combine(temp.Root, "config-as-directory");
        Directory.CreateDirectory(configDirectoryPath);
        var service = CreateService(configDirectoryPath, temp.StatusRoot);

        var result = service.AddJob(Job("backup", "Backup", "backup/status.json"));

        Assert.False(result.Succeeded);
        Assert.Equal("Could not write jobs config.", result.ErrorMessage);
        Assert.Empty(Directory.EnumerateFiles(temp.Root, "config-as-directory.tmp.*"));
    }

    [Fact]
    public void AddJob_RejectsStatusPathTraversalForStarterStatusCreation()
    {
        using var temp = TestTempDirectory.Create();
        var outsidePath = Path.Combine(temp.Root, "outside", "status.json");
        var service = CreateService(temp.ConfigPath, temp.StatusRoot);

        var result = service.AddJob(Job("escape", "Escape", "../outside/status.json"));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("Job was saved, but starter status.json could not be created.", result.StatusWarning);
        Assert.False(File.Exists(outsidePath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(outsidePath)!));
    }

    [Fact]
    public void AddJob_CreatesStarterStatusInsideConfiguredStatusRoot()
    {
        using var temp = TestTempDirectory.Create();
        var service = CreateService(temp.ConfigPath, temp.StatusRoot);

        var result = service.AddJob(Job("backup", "Backup", "nested/backup/status.json"));

        Assert.True(result.Succeeded, result.ErrorMessage);
        var statusPath = Path.Combine(temp.StatusRoot, "nested", "backup", "status.json");
        Assert.True(File.Exists(statusPath));
        Assert.Equal(
            "{\n" +
            "  \"status\": \"unknown\",\n" +
            "  \"last_run\": null,\n" +
            "  \"runtime\": \"-\",\n" +
            "  \"message\": \"Job has not reported status yet.\",\n" +
            "  \"warnings\": 0,\n" +
            "  \"errors\": 0\n" +
            "}",
            Normalize(File.ReadAllText(statusPath)).TrimEnd());
        Assert.DoesNotContain(Directory.EnumerateFiles(temp.Root, "status.json", SearchOption.AllDirectories), path => !path.StartsWith(temp.StatusRoot, StringComparison.Ordinal));
    }

    [Fact]
    public void DeleteJob_RemovesOnlySelectedJob()
    {
        using var temp = TestTempDirectory.Create();
        File.WriteAllText(temp.ConfigPath,
            "jobs:\n" +
            "- id: keep\n" +
            "  name: Keep\n" +
            "  status_path: keep/status.json\n" +
            "  stale_after_minutes: 60\n" +
            "- id: remove\n" +
            "  name: Remove\n" +
            "  status_path: remove/status.json\n" +
            "  stale_after_minutes: 30\n");
        var service = CreateService(temp.ConfigPath, temp.StatusRoot);

        var result = service.DeleteJob("remove", deleteStatusFile: false, forceDelete: false);

        Assert.True(result.Succeeded, result.ErrorMessage);
        var yaml = File.ReadAllText(temp.ConfigPath);
        Assert.Contains("id: keep", yaml);
        Assert.DoesNotContain("id: remove", yaml);
    }

    [Fact]
    public void DeleteJob_OptionalStatusCleanupDeletesOnlyIntendedStatusJson()
    {
        using var temp = TestTempDirectory.Create();
        File.WriteAllText(temp.ConfigPath,
            "jobs:\n" +
            "- id: remove\n" +
            "  name: Remove\n" +
            "  status_path: remove/status.json\n" +
            "  stale_after_minutes: 60\n");
        var intendedStatusPath = Path.Combine(temp.StatusRoot, "remove", "status.json");
        var siblingStatusPath = Path.Combine(temp.StatusRoot, "remove", "other-status.json");
        Directory.CreateDirectory(Path.GetDirectoryName(intendedStatusPath)!);
        File.WriteAllText(intendedStatusPath, "{}");
        File.WriteAllText(siblingStatusPath, "{}\n");
        var service = CreateService(temp.ConfigPath, temp.StatusRoot);

        var result = service.DeleteJob("remove", deleteStatusFile: true, forceDelete: false);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(File.Exists(intendedStatusPath));
        Assert.True(File.Exists(siblingStatusPath));
    }

    [Fact]
    public void DeleteJob_OptionalStatusCleanupDeletesOnlyEmptyParentFolder()
    {
        using var temp = TestTempDirectory.Create();
        File.WriteAllText(temp.ConfigPath,
            "jobs:\n" +
            "- id: remove\n" +
            "  name: Remove\n" +
            "  status_path: parent/child/status.json\n" +
            "  stale_after_minutes: 60\n");
        var parentPath = Path.Combine(temp.StatusRoot, "parent");
        var childPath = Path.Combine(parentPath, "child");
        var statusPath = Path.Combine(childPath, "status.json");
        Directory.CreateDirectory(childPath);
        File.WriteAllText(statusPath, "{}");
        var service = CreateService(temp.ConfigPath, temp.StatusRoot);

        var result = service.DeleteJob("remove", deleteStatusFile: true, forceDelete: false);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(File.Exists(statusPath));
        Assert.False(Directory.Exists(childPath));
        Assert.True(Directory.Exists(parentPath));
    }

    [Fact]
    public void DeleteJob_OptionalStatusCleanupDoesNotDeleteNonEmptyParentFolder()
    {
        using var temp = TestTempDirectory.Create();
        File.WriteAllText(temp.ConfigPath,
            "jobs:\n" +
            "- id: remove\n" +
            "  name: Remove\n" +
            "  status_path: remove/status.json\n" +
            "  stale_after_minutes: 60\n");
        var parentPath = Path.Combine(temp.StatusRoot, "remove");
        var statusPath = Path.Combine(parentPath, "status.json");
        var markerPath = Path.Combine(parentPath, "keep.txt");
        Directory.CreateDirectory(parentPath);
        File.WriteAllText(statusPath, "{}");
        File.WriteAllText(markerPath, "keep");
        var service = CreateService(temp.ConfigPath, temp.StatusRoot);

        var result = service.DeleteJob("remove", deleteStatusFile: true, forceDelete: false);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(File.Exists(statusPath));
        Assert.True(File.Exists(markerPath));
        Assert.True(Directory.Exists(parentPath));
    }

    private static JobConfigWriterService CreateService(string configPath, string statusRoot)
    {
        return new JobConfigWriterService(Options.Create(new OpsSlatePathOptions
        {
            ConfigPath = configPath,
            StatusRoot = statusRoot
        }));
    }

    private static JobConfig Job(string id, string name, string statusPath) => new()
    {
        Id = id,
        Name = name,
        StatusPath = statusPath,
        StaleAfterMinutes = 60
    };

    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);
}
