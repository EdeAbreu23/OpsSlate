using System.Text.Json;
using HomelabAutomationCenter.Models;
using HomelabAutomationCenter.Options;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HomelabAutomationCenter.Services;

public sealed class JobConfigWriterService
{
    private static readonly JsonSerializerOptions StarterStatusJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private readonly HacPathOptions _pathOptions;

    public JobConfigWriterService(IOptions<HacPathOptions> pathOptions)
    {
        _pathOptions = pathOptions.Value;
    }

    private sealed class JobsFile
    {
        public List<WritableJobConfig> Jobs { get; set; } = [];
    }

    private sealed class WritableJobConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string StatusPath { get; set; } = string.Empty;
        public int StaleAfterMinutes { get; set; } = 60;
        public List<string>? DependsOn { get; set; }
    }

    private sealed class StarterStatus
    {
        public string Status { get; set; } = "unknown";
        public object? LastRun { get; set; }
        public string Runtime { get; set; } = "-";
        public string Message { get; set; } = "Job has not reported status yet.";
        public int Warnings { get; set; }
        public int Errors { get; set; }
    }

    public WriteJobConfigResult AddJob(JobConfig job)
    {
        string yaml;
        try
        {
            yaml = BuildUpdatedYaml(job);
        }
        catch (Exception ex)
        {
            return WriteJobConfigResult.Failure($"Could not read existing jobs config at {_pathOptions.ConfigPath}: {Concise(ex.Message)}");
        }

        var backupPath = BackupPath(_pathOptions.ConfigPath);
        var tempPath = $"{_pathOptions.ConfigPath}.tmp.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";

        try
        {
            WriteYamlWithBackup(yaml, backupPath, tempPath);
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            return WriteJobConfigResult.Failure($"Could not write jobs config at {_pathOptions.ConfigPath}: {Concise(ex.Message)}");
        }

        var statusWarning = TryCreateStarterStatus(job.StatusPath);
        return WriteJobConfigResult.Success(backupPath, statusWarning);
    }

    public DeleteJobConfigResult DeleteJob(string jobId, bool deleteStatusFile, bool forceDelete)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return DeleteJobConfigResult.Failure("Job ID is required.");
        }

        JobsFile jobsFile;
        try
        {
            jobsFile = ReadWritableJobsFile();
        }
        catch (Exception ex)
        {
            return DeleteJobConfigResult.Failure($"Could not read jobs config at {_pathOptions.ConfigPath}: {Concise(ex.Message)}");
        }

        var job = jobsFile.Jobs.FirstOrDefault(job => string.Equals(job.Id, jobId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (job is null)
        {
            return DeleteJobConfigResult.Failure($"Job '{jobId.Trim()}' does not exist.");
        }

        var dependentJobs = FindDependentJobIds(jobsFile.Jobs, job.Id).ToList();
        if (dependentJobs.Count > 0 && !forceDelete)
        {
            return DeleteJobConfigResult.Failure($"Cannot delete '{job.Id}' because these jobs depend on it: {string.Join(", ", dependentJobs)}.");
        }

        var updatedJobs = jobsFile.Jobs
            .Where(existingJob => !string.Equals(existingJob.Id, job.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var yaml = CreateSerializer().Serialize(new JobsFile { Jobs = updatedJobs });
        var backupPath = BackupPath(_pathOptions.ConfigPath);
        var tempPath = $"{_pathOptions.ConfigPath}.tmp.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";

        try
        {
            WriteYamlWithBackup(yaml, backupPath, tempPath);
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            return DeleteJobConfigResult.Failure($"Could not write jobs config at {_pathOptions.ConfigPath}: {Concise(ex.Message)}");
        }

        var statusCleanupMessage = deleteStatusFile
            ? TryDeleteStatusFile(job.StatusPath)
            : "Status file cleanup was not requested.";

        return DeleteJobConfigResult.Success(backupPath, statusCleanupMessage);
    }

    private string BuildUpdatedYaml(JobConfig job)
    {
        var serializer = CreateSerializer();
        var newJob = new WritableJobConfig
        {
            Id = job.Id,
            Name = job.Name,
            StatusPath = job.StatusPath,
            StaleAfterMinutes = job.StaleAfterMinutes,
            DependsOn = job.DependsOn.Count > 0 ? job.DependsOn : null
        };

        if (!File.Exists(_pathOptions.ConfigPath))
        {
            return serializer.Serialize(new JobsFile { Jobs = [newJob] });
        }

        var existingYaml = File.ReadAllText(_pathOptions.ConfigPath);
        if (string.IsNullOrWhiteSpace(existingYaml))
        {
            return serializer.Serialize(new JobsFile { Jobs = [newJob] });
        }

        var deserializer = CreateDeserializer();
        var existingJobs = deserializer.Deserialize<JobsFile>(existingYaml);
        if (existingJobs?.Jobs is null || existingJobs.Jobs.Count == 0)
        {
            return serializer.Serialize(new JobsFile { Jobs = [newJob] });
        }

        return AppendJob(existingYaml, newJob, serializer);
    }

    private JobsFile ReadWritableJobsFile()
    {
        if (!File.Exists(_pathOptions.ConfigPath))
        {
            return new JobsFile();
        }

        var yaml = File.ReadAllText(_pathOptions.ConfigPath);
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return new JobsFile();
        }

        var jobsFile = CreateDeserializer().Deserialize<JobsFile>(yaml);
        return new JobsFile
        {
            Jobs = jobsFile?.Jobs?
                .Where(job => !string.IsNullOrWhiteSpace(job.Id))
                .Select(job => new WritableJobConfig
                {
                    Id = job.Id.Trim(),
                    Name = job.Name.Trim(),
                    StatusPath = job.StatusPath.Trim(),
                    StaleAfterMinutes = job.StaleAfterMinutes <= 0 ? 60 : job.StaleAfterMinutes,
                    DependsOn = job.DependsOn?
                        .Where(dependency => !string.IsNullOrWhiteSpace(dependency))
                        .Select(dependency => dependency.Trim())
                        .ToList()
                })
                .ToList() ?? []
        };
    }

    private static IEnumerable<string> FindDependentJobIds(IEnumerable<WritableJobConfig> jobs, string jobId)
    {
        return jobs
            .Where(job => job.DependsOn?.Any(dependency => string.Equals(dependency, jobId, StringComparison.OrdinalIgnoreCase)) == true)
            .Select(job => job.Id)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase);
    }

    private void WriteYamlWithBackup(string yaml, string backupPath, string tempPath)
    {
        var configDirectory = Path.GetDirectoryName(_pathOptions.ConfigPath);
        if (!string.IsNullOrWhiteSpace(configDirectory))
        {
            Directory.CreateDirectory(configDirectory);
        }

        File.WriteAllText(tempPath, yaml);

        if (File.Exists(_pathOptions.ConfigPath))
        {
            File.Copy(_pathOptions.ConfigPath, backupPath, overwrite: false);
        }

        File.Move(tempPath, _pathOptions.ConfigPath, overwrite: true);
    }

    private string TryDeleteStatusFile(string statusPath)
    {
        var resolvedStatusPath = _pathOptions.ResolveStatusPath(statusPath);
        try
        {
            if (!File.Exists(resolvedStatusPath))
            {
                return $"Status file was not found at {resolvedStatusPath}; no status file was deleted.";
            }

            File.Delete(resolvedStatusPath);

            var statusDirectory = Path.GetDirectoryName(resolvedStatusPath);
            if (string.IsNullOrWhiteSpace(statusDirectory) || !Directory.Exists(statusDirectory))
            {
                return $"Deleted status file at {resolvedStatusPath}.";
            }

            if (Directory.EnumerateFileSystemEntries(statusDirectory).Any())
            {
                return $"Deleted status file at {resolvedStatusPath}. Parent folder was not deleted because it is not empty.";
            }

            Directory.Delete(statusDirectory, recursive: false);
            return $"Deleted status file at {resolvedStatusPath} and removed the empty parent folder {statusDirectory}.";
        }
        catch (Exception ex)
        {
            return $"Job was deleted, but status cleanup at {resolvedStatusPath} did not complete: {Concise(ex.Message)}";
        }
    }

    private static string AppendJob(string existingYaml, WritableJobConfig newJob, ISerializer serializer)
    {
        var serializedJob = serializer.Serialize(new[] { newJob });
        var indentedJob = string.Join(
            Environment.NewLine,
            serializedJob.TrimEnd().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line => $"  {line}"));

        return $"{existingYaml.TrimEnd()}{Environment.NewLine}{indentedJob}{Environment.NewLine}";
    }

    private string? TryCreateStarterStatus(string statusPath)
    {
        var resolvedStatusPath = _pathOptions.ResolveStatusPath(statusPath);
        try
        {
            var statusDirectory = Path.GetDirectoryName(resolvedStatusPath);
            if (!string.IsNullOrWhiteSpace(statusDirectory))
            {
                Directory.CreateDirectory(statusDirectory);
            }

            if (!File.Exists(resolvedStatusPath))
            {
                var json = JsonSerializer.Serialize(new StarterStatus(), StarterStatusJsonOptions);
                File.WriteAllText(resolvedStatusPath, json);
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"Job was saved, but starter status.json could not be created at {resolvedStatusPath}: {Concise(ex.Message)}";
        }
    }

    private static ISerializer CreateSerializer()
    {
        return new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
    }

    private static IDeserializer CreateDeserializer()
    {
        return new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    private static string BackupPath(string configPath)
    {
        return $"{configPath}.bak.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup only. The original config file is not modified by this path.
        }
    }

    private static string Concise(string message)
    {
        return string.Join(" ", message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
    }
}

public sealed class WriteJobConfigResult
{
    private WriteJobConfigResult(bool succeeded, string? errorMessage, string? backupPath, string? statusWarning)
    {
        Succeeded = succeeded;
        ErrorMessage = errorMessage;
        BackupPath = backupPath;
        StatusWarning = statusWarning;
    }

    public bool Succeeded { get; }
    public string? ErrorMessage { get; }
    public string? BackupPath { get; }
    public string? StatusWarning { get; }

    public static WriteJobConfigResult Success(string backupPath, string? statusWarning)
    {
        return new WriteJobConfigResult(true, null, backupPath, statusWarning);
    }

    public static WriteJobConfigResult Failure(string errorMessage)
    {
        return new WriteJobConfigResult(false, errorMessage, null, null);
    }
}

public sealed class DeleteJobConfigResult
{
    private DeleteJobConfigResult(bool succeeded, string? errorMessage, string? backupPath, string? statusCleanupMessage)
    {
        Succeeded = succeeded;
        ErrorMessage = errorMessage;
        BackupPath = backupPath;
        StatusCleanupMessage = statusCleanupMessage;
    }

    public bool Succeeded { get; }
    public string? ErrorMessage { get; }
    public string? BackupPath { get; }
    public string? StatusCleanupMessage { get; }

    public static DeleteJobConfigResult Success(string backupPath, string? statusCleanupMessage)
    {
        return new DeleteJobConfigResult(true, null, backupPath, statusCleanupMessage);
    }

    public static DeleteJobConfigResult Failure(string errorMessage)
    {
        return new DeleteJobConfigResult(false, errorMessage, null, null);
    }
}
