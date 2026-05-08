using System.Text.Json;
using HomelabAutomationCenter.Options;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HomelabAutomationCenter.Services;

public sealed class JobConfigEditService
{
    private static readonly JsonSerializerOptions StarterStatusJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private readonly HacPathOptions _pathOptions;

    public JobConfigEditService(IOptions<HacPathOptions> pathOptions)
    {
        _pathOptions = pathOptions.Value;
    }

    private sealed class JobsFile
    {
        public List<EditableJobConfig> Jobs { get; set; } = [];
    }

    private sealed class EditableJobConfig
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

    public EditableJob? GetJob(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return null;
        }

        var jobsFile = ReadJobsFile();
        var job = jobsFile.Jobs.FirstOrDefault(job => string.Equals(job.Id, jobId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (job is null)
        {
            return null;
        }

        return new EditableJob(
            job.Id,
            job.Name,
            job.StatusPath,
            job.StaleAfterMinutes <= 0 ? 60 : job.StaleAfterMinutes,
            job.DependsOn?.Where(dependency => !string.IsNullOrWhiteSpace(dependency)).Select(dependency => dependency.Trim()).ToList() ?? []);
    }

    public EditJobConfigResult UpdateJob(EditableJob editedJob)
    {
        if (string.IsNullOrWhiteSpace(editedJob.Id))
        {
            return EditJobConfigResult.Failure("Job ID is required.");
        }

        JobsFile jobsFile;
        try
        {
            jobsFile = ReadJobsFile();
        }
        catch (Exception ex)
        {
            return EditJobConfigResult.Failure($"Could not read jobs config at {_pathOptions.ConfigPath}: {Concise(ex.Message)}");
        }

        var jobIndex = jobsFile.Jobs.FindIndex(job => string.Equals(job.Id, editedJob.Id.Trim(), StringComparison.OrdinalIgnoreCase));
        if (jobIndex < 0)
        {
            return EditJobConfigResult.Failure($"Job '{editedJob.Id.Trim()}' does not exist.");
        }

        var originalStatusPath = jobsFile.Jobs[jobIndex].StatusPath;
        jobsFile.Jobs[jobIndex] = new EditableJobConfig
        {
            Id = jobsFile.Jobs[jobIndex].Id,
            Name = editedJob.Name.Trim(),
            StatusPath = editedJob.StatusPath.Trim(),
            StaleAfterMinutes = editedJob.StaleAfterMinutes,
            DependsOn = editedJob.DependsOn.Count > 0 ? editedJob.DependsOn.Select(dependency => dependency.Trim()).ToList() : null
        };

        var yaml = CreateSerializer().Serialize(jobsFile);
        var backupPath = BackupPath(_pathOptions.ConfigPath);
        var tempPath = $"{_pathOptions.ConfigPath}.tmp.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";

        try
        {
            WriteYamlWithBackup(yaml, backupPath, tempPath);
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            return EditJobConfigResult.Failure($"Could not write jobs config at {_pathOptions.ConfigPath}: {Concise(ex.Message)}");
        }

        var statusWarning = string.Equals(originalStatusPath, editedJob.StatusPath, StringComparison.Ordinal)
            ? null
            : TryCreateStarterStatus(editedJob.StatusPath);

        return EditJobConfigResult.Success(backupPath, statusWarning);
    }

    private JobsFile ReadJobsFile()
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
                .Select(job => new EditableJobConfig
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

public sealed record EditableJob(
    string Id,
    string Name,
    string StatusPath,
    int StaleAfterMinutes,
    IReadOnlyList<string> DependsOn);

public sealed class EditJobConfigResult
{
    private EditJobConfigResult(bool succeeded, string? errorMessage, string? backupPath, string? statusWarning)
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

    public static EditJobConfigResult Success(string backupPath, string? statusWarning)
    {
        return new EditJobConfigResult(true, null, backupPath, statusWarning);
    }

    public static EditJobConfigResult Failure(string errorMessage)
    {
        return new EditJobConfigResult(false, errorMessage, null, null);
    }
}
