using Microsoft.Extensions.Options;
using OpsSlate.Options;
using OpsSlate.Services;
using Xunit;
using OpsSlate.Tests;

namespace OpsSlate.Tests.Services;

public sealed class JobConfigEditServiceTests
{
    [Fact]
    public void UpdateJob_UpdatesExpectedYaml()
    {
        using var temp = TestTempDirectory.Create();
        File.WriteAllText(temp.ConfigPath,
            "jobs:\n" +
            "- id: backup\n" +
            "  name: Backup\n" +
            "  status_path: backup/status.json\n" +
            "  stale_after_minutes: 60\n" +
            "  depends_on:\n" +
            "  - old-dependency\n");
        var service = CreateService(temp.ConfigPath, temp.StatusRoot);

        var result = service.UpdateJob(new EditableJob(
            "backup",
            "Nightly Backup",
            "nightly/status.json",
            15,
            ["database", "storage"]));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(
            "jobs:\n" +
            "- id: backup\n" +
            "  name: Nightly Backup\n" +
            "  status_path: nightly/status.json\n" +
            "  stale_after_minutes: 15\n" +
            "  depends_on:\n" +
            "  - database\n" +
            "  - storage\n" +
            "...\n",
            Normalize(File.ReadAllText(temp.ConfigPath)));
    }

    [Fact]
    public void UpdateJob_CreatesTimestampBackupBeforeWriteWhenConfigExists()
    {
        using var temp = TestTempDirectory.Create();
        var originalYaml =
            "jobs:\n" +
            "- id: backup\n" +
            "  name: Backup\n" +
            "  status_path: backup/status.json\n" +
            "  stale_after_minutes: 60\n";
        File.WriteAllText(temp.ConfigPath, originalYaml);
        var service = CreateService(temp.ConfigPath, temp.StatusRoot);

        var result = service.UpdateJob(new EditableJob("backup", "Backup Updated", "backup/status.json", 30, []));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(result.BackupPath);
        Assert.Matches(@"jobs\.yml\.bak\.\d{17}$", result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));
        Assert.Equal(originalYaml, File.ReadAllText(result.BackupPath));
        Assert.Contains("name: Backup Updated", File.ReadAllText(temp.ConfigPath));
    }

    [Fact]
    public void UpdateJob_RejectsStatusPathTraversalForStarterStatusCreation()
    {
        using var temp = TestTempDirectory.Create();
        File.WriteAllText(temp.ConfigPath,
            "jobs:\n" +
            "- id: backup\n" +
            "  name: Backup\n" +
            "  status_path: backup/status.json\n" +
            "  stale_after_minutes: 60\n");
        var outsidePath = Path.Combine(temp.Root, "outside", "status.json");
        var service = CreateService(temp.ConfigPath, temp.StatusRoot);

        var result = service.UpdateJob(new EditableJob("backup", "Backup", "../outside/status.json", 60, []));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("Job was saved, but starter status.json could not be created.", result.StatusWarning);
        Assert.False(File.Exists(outsidePath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(outsidePath)!));
    }

    [Fact]
    public void UpdateJob_CreatesStarterStatusInsideConfiguredStatusRootWhenStatusPathChanges()
    {
        using var temp = TestTempDirectory.Create();
        File.WriteAllText(temp.ConfigPath,
            "jobs:\n" +
            "- id: backup\n" +
            "  name: Backup\n" +
            "  status_path: backup/status.json\n" +
            "  stale_after_minutes: 60\n");
        var service = CreateService(temp.ConfigPath, temp.StatusRoot);

        var result = service.UpdateJob(new EditableJob("backup", "Backup", "new/status.json", 60, []));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(File.Exists(Path.Combine(temp.StatusRoot, "new", "status.json")));
        Assert.DoesNotContain(Directory.EnumerateFiles(temp.Root, "status.json", SearchOption.AllDirectories), path => !path.StartsWith(temp.StatusRoot, StringComparison.Ordinal));
    }

    private static JobConfigEditService CreateService(string configPath, string statusRoot)
    {
        return new JobConfigEditService(Options.Create(new OpsSlatePathOptions
        {
            ConfigPath = configPath,
            StatusRoot = statusRoot
        }));
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);
}
