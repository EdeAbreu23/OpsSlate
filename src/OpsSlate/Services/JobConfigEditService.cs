using System.Text.Json;
using OpsSlate.Options;
using Microsoft.Extensions.Options;
using YamlDotNet.RepresentationModel;

namespace OpsSlate.Services;

public sealed class JobConfigEditService
{
    private static readonly JsonSerializerOptions StarterStatusJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private readonly OpsSlatePathOptions _pathOptions;

    public JobConfigEditService(IOptions<OpsSlatePathOptions> pathOptions)
    {
        _pathOptions = pathOptions.Value;
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

        var jobsSequence = ReadJobsSequence();
        var job = FindJob(jobsSequence, jobId.Trim());
        if (job is null)
        {
            return null;
        }

        return new EditableJob(
            GetScalarValue(job, "id"),
            GetScalarValue(job, "name"),
            GetScalarValue(job, "status_path"),
            GetPositiveIntValue(job, "stale_after_minutes", 60),
            GetStringSequenceValues(job, "depends_on"));
    }

    public EditJobConfigResult UpdateJob(EditableJob editedJob)
    {
        if (string.IsNullOrWhiteSpace(editedJob.Id))
        {
            return EditJobConfigResult.Failure("Job ID is required.");
        }

        YamlStream yamlStream;
        YamlSequenceNode jobsSequence;
        try
        {
            (yamlStream, jobsSequence) = ReadJobsDocument();
        }
        catch (Exception)
        {
            return EditJobConfigResult.Failure("Could not read jobs config.");
        }

        var job = FindJob(jobsSequence, editedJob.Id.Trim());
        if (job is null)
        {
            return EditJobConfigResult.Failure($"Job '{editedJob.Id.Trim()}' does not exist.");
        }

        var originalStatusPath = GetScalarValue(job, "status_path");
        SetScalarValue(job, "name", editedJob.Name.Trim());
        SetScalarValue(job, "status_path", editedJob.StatusPath.Trim());
        SetScalarValue(job, "stale_after_minutes", editedJob.StaleAfterMinutes.ToString());
        SetDependsOn(job, editedJob.DependsOn);

        var yaml = SaveYaml(yamlStream);
        var backupPath = BackupPath(_pathOptions.ConfigPath);
        var tempPath = $"{_pathOptions.ConfigPath}.tmp.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";

        try
        {
            WriteYamlWithBackup(yaml, backupPath, tempPath);
        }
        catch (Exception)
        {
            TryDelete(tempPath);
            return EditJobConfigResult.Failure("Could not write jobs config.");
        }

        var statusWarning = string.Equals(originalStatusPath, editedJob.StatusPath, StringComparison.Ordinal)
            ? null
            : TryCreateStarterStatus(editedJob.StatusPath);

        return EditJobConfigResult.Success(backupPath, statusWarning);
    }

    private YamlSequenceNode ReadJobsSequence()
    {
        var (_, jobsSequence) = ReadJobsDocument();
        return jobsSequence;
    }

    private (YamlStream YamlStream, YamlSequenceNode JobsSequence) ReadJobsDocument()
    {
        if (!File.Exists(_pathOptions.ConfigPath))
        {
            return CreateEmptyJobsDocument();
        }

        var yaml = File.ReadAllText(_pathOptions.ConfigPath);
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return CreateEmptyJobsDocument();
        }

        var yamlStream = new YamlStream();
        using var reader = new StringReader(yaml);
        yamlStream.Load(reader);

        if (yamlStream.Documents.Count == 0)
        {
            return CreateEmptyJobsDocument();
        }

        if (yamlStream.Documents[0].RootNode is not YamlMappingNode rootNode)
        {
            throw new InvalidOperationException("The jobs config root must be a YAML mapping.");
        }

        var jobsNode = GetChildNode(rootNode, "jobs");
        if (jobsNode is null)
        {
            var jobsSequence = new YamlSequenceNode();
            rootNode.Children.Add(new YamlScalarNode("jobs"), jobsSequence);
            return (yamlStream, jobsSequence);
        }

        if (jobsNode is not YamlSequenceNode existingJobsSequence)
        {
            throw new InvalidOperationException("The jobs config 'jobs' value must be a YAML sequence.");
        }

        return (yamlStream, existingJobsSequence);
    }

    private static (YamlStream YamlStream, YamlSequenceNode JobsSequence) CreateEmptyJobsDocument()
    {
        var jobsSequence = new YamlSequenceNode();
        var rootNode = new YamlMappingNode();
        rootNode.Children.Add(new YamlScalarNode("jobs"), jobsSequence);
        var yamlStream = new YamlStream(new YamlDocument(rootNode));
        return (yamlStream, jobsSequence);
    }

    private static YamlMappingNode? FindJob(YamlSequenceNode jobsSequence, string jobId)
    {
        return jobsSequence.Children
            .OfType<YamlMappingNode>()
            .FirstOrDefault(job => string.Equals(GetScalarValue(job, "id"), jobId, StringComparison.OrdinalIgnoreCase));
    }

    private static YamlNode? GetChildNode(YamlMappingNode mappingNode, string key)
    {
        return mappingNode.Children.FirstOrDefault(child => IsScalarKey(child.Key, key)).Value;
    }

    private static string GetScalarValue(YamlMappingNode mappingNode, string key)
    {
        return GetChildNode(mappingNode, key) is YamlScalarNode scalarNode
            ? (scalarNode.Value ?? string.Empty).Trim()
            : string.Empty;
    }

    private static int GetPositiveIntValue(YamlMappingNode mappingNode, string key, int defaultValue)
    {
        return int.TryParse(GetScalarValue(mappingNode, key), out var value) && value > 0
            ? value
            : defaultValue;
    }

    private static IReadOnlyList<string> GetStringSequenceValues(YamlMappingNode mappingNode, string key)
    {
        return GetChildNode(mappingNode, key) is YamlSequenceNode sequenceNode
            ? sequenceNode.Children
                .OfType<YamlScalarNode>()
                .Select(dependency => dependency.Value?.Trim() ?? string.Empty)
                .Where(dependency => !string.IsNullOrWhiteSpace(dependency))
                .ToList()
            : [];
    }

    private static void SetScalarValue(YamlMappingNode mappingNode, string key, string value)
    {
        var existingKey = FindKey(mappingNode, key);
        if (existingKey is not null)
        {
            mappingNode.Children[existingKey] = new YamlScalarNode(value);
            return;
        }

        mappingNode.Children.Add(new YamlScalarNode(key), new YamlScalarNode(value));
    }

    private static void SetDependsOn(YamlMappingNode mappingNode, IReadOnlyList<string> dependencies)
    {
        var existingKey = FindKey(mappingNode, "depends_on");
        if (dependencies.Count == 0)
        {
            if (existingKey is not null)
            {
                mappingNode.Children.Remove(existingKey);
            }

            return;
        }

        var sequenceNode = new YamlSequenceNode();
        foreach (var dependency in dependencies)
        {
            sequenceNode.Children.Add(new YamlScalarNode(dependency.Trim()));
        }
        if (existingKey is not null)
        {
            mappingNode.Children[existingKey] = sequenceNode;
            return;
        }

        mappingNode.Children.Add(new YamlScalarNode("depends_on"), sequenceNode);
    }

    private static YamlNode? FindKey(YamlMappingNode mappingNode, string key)
    {
        return mappingNode.Children.Keys.FirstOrDefault(existingKey => IsScalarKey(existingKey, key));
    }

    private static bool IsScalarKey(YamlNode node, string key)
    {
        return node is YamlScalarNode scalarNode && string.Equals(scalarNode.Value, key, StringComparison.Ordinal);
    }

    private static string SaveYaml(YamlStream yamlStream)
    {
        using var writer = new StringWriter();
        yamlStream.Save(writer, false);
        return writer.ToString();
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
        try
        {
            var resolvedStatusPath = _pathOptions.ResolveStatusPath(statusPath);
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
        catch (Exception)
        {
            return "Job was saved, but starter status.json could not be created.";
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
        return string.Join(" ", message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
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
