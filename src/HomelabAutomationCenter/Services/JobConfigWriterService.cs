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
        catch (Exception ex)
        {
            TryDelete(tempPath);
            return WriteJobConfigResult.Failure($"Could not write jobs config at {_pathOptions.ConfigPath}: {Concise(ex.Message)}");
        }

        var statusWarning = TryCreateStarterStatus(job.StatusPath);
        return WriteJobConfigResult.Success(backupPath, statusWarning);
    }

    private string BuildUpdatedYaml(JobConfig job)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
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

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var existingJobs = deserializer.Deserialize<JobsFile>(existingYaml);
        if (existingJobs?.Jobs is null || existingJobs.Jobs.Count == 0)
        {
            return serializer.Serialize(new JobsFile { Jobs = [newJob] });
        }

        return AppendJob(existingYaml, newJob, serializer);
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
