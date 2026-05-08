using System.Text.Json;
using HomelabAutomationCenter.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HomelabAutomationCenter.Services;

public sealed class SystemValidationService
{
    private const string ConfigPath = "/config/jobs.yml";

    private sealed class JobsFile
    {
        public List<JobConfig>? Jobs { get; set; }
    }

    public IReadOnlyList<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();

        if (!File.Exists(ConfigPath))
        {
            Add(results, ValidationStatus.Error, "jobs.yml exists", $"{ConfigPath} was not found.");
            return results;
        }

        Add(results, ValidationStatus.Pass, "jobs.yml exists", $"{ConfigPath} was found.");

        JobsFile? jobsFile;
        try
        {
            var yaml = File.ReadAllText(ConfigPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            jobsFile = deserializer.Deserialize<JobsFile>(yaml);
            Add(results, ValidationStatus.Pass, "jobs.yml can be parsed", "Dashboard configuration YAML parsed successfully.");
        }
        catch (Exception ex)
        {
            Add(results, ValidationStatus.Error, "jobs.yml can be parsed", $"YAML parsing failed: {ex.Message}");
            return results;
        }

        var jobs = jobsFile?.Jobs ?? [];
        if (jobs.Count == 0)
        {
            Add(results, ValidationStatus.Error, "At least one job is configured", "No jobs were found in the jobs list.");
            return results;
        }

        Add(results, ValidationStatus.Pass, "At least one job is configured", $"Found {jobs.Count} configured job(s).");

        ValidateJobFields(results, jobs);
        ValidateDependencies(results, jobs);
        ValidateStatusFiles(results, jobs);

        return results;
    }

    private static void ValidateJobFields(List<ValidationResult> results, IReadOnlyList<JobConfig> jobs)
    {
        var ids = jobs.Select(job => job.Id).ToList();
        var emptyIdIndexes = ids
            .Select((id, index) => new { id, index })
            .Where(item => string.IsNullOrWhiteSpace(item.id))
            .Select(item => item.index + 1)
            .ToList();

        if (emptyIdIndexes.Count > 0)
        {
            Add(results, ValidationStatus.Error, "Job IDs are not empty", $"Job entry #{string.Join(", #", emptyIdIndexes)} has an empty id.");
        }
        else
        {
            Add(results, ValidationStatus.Pass, "Job IDs are not empty", "Every configured job has an id.");
        }

        var duplicateIds = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .GroupBy(id => id.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            Add(results, ValidationStatus.Error, "Job IDs are unique", $"Duplicate job id(s), ignoring case: {string.Join(", ", duplicateIds)}.");
        }
        else
        {
            Add(results, ValidationStatus.Pass, "Job IDs are unique", "No duplicate job ids were found using case-insensitive comparison.");
        }

        AddFieldValidation(
            results,
            jobs,
            "Job names are not empty",
            job => job.Name,
            "name",
            "Every configured job has a name.");

        AddFieldValidation(
            results,
            jobs,
            "status_path is not empty",
            job => job.StatusPath,
            "status_path",
            "Every configured job has a status_path.");

        var invalidStaleJobs = jobs
            .Where(job => job.StaleAfterMinutes <= 0)
            .Select(DisplayJob)
            .ToList();

        if (invalidStaleJobs.Count > 0)
        {
            Add(results, ValidationStatus.Error, "stale_after_minutes is greater than 0", $"Invalid stale_after_minutes for: {string.Join(", ", invalidStaleJobs)}.");
        }
        else
        {
            Add(results, ValidationStatus.Pass, "stale_after_minutes is greater than 0", "Every configured job has a positive stale_after_minutes value.");
        }
    }

    private static void ValidateDependencies(List<ValidationResult> results, IReadOnlyList<JobConfig> jobs)
    {
        var idSet = jobs
            .Where(job => !string.IsNullOrWhiteSpace(job.Id))
            .Select(job => job.Id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingReferences = new List<string>();
        var selfReferences = new List<string>();

        foreach (var job in jobs.Where(job => !string.IsNullOrWhiteSpace(job.Id)))
        {
            foreach (var dependency in job.DependsOn.Where(dependency => !string.IsNullOrWhiteSpace(dependency)))
            {
                var trimmedDependency = dependency.Trim();
                if (!idSet.Contains(trimmedDependency))
                {
                    missingReferences.Add($"{DisplayJob(job)} -> {trimmedDependency}");
                }

                if (string.Equals(job.Id.Trim(), trimmedDependency, StringComparison.OrdinalIgnoreCase))
                {
                    selfReferences.Add(DisplayJob(job));
                }
            }
        }

        if (missingReferences.Count > 0)
        {
            Add(results, ValidationStatus.Error, "depends_on entries reference existing job IDs", $"Missing dependency reference(s): {string.Join(", ", missingReferences)}.");
        }
        else
        {
            Add(results, ValidationStatus.Pass, "depends_on entries reference existing job IDs", "Every depends_on entry points at a configured job id.");
        }

        if (selfReferences.Count > 0)
        {
            Add(results, ValidationStatus.Error, "depends_on does not reference itself", $"Self dependency found for: {string.Join(", ", selfReferences.Distinct(StringComparer.OrdinalIgnoreCase))}.");
        }
        else
        {
            Add(results, ValidationStatus.Pass, "depends_on does not reference itself", "No job depends on itself.");
        }

        var cycles = FindDependencyCycles(jobs);
        if (cycles.Count > 0)
        {
            Add(results, ValidationStatus.Error, "Circular dependency detection", $"Dependency cycle(s) found: {string.Join("; ", cycles)}.");
        }
        else
        {
            Add(results, ValidationStatus.Pass, "Circular dependency detection", "No circular dependencies were found among configured jobs.");
        }
    }

    private static List<string> FindDependencyCycles(IReadOnlyList<JobConfig> jobs)
    {
        var graph = jobs
            .Where(job => !string.IsNullOrWhiteSpace(job.Id))
            .GroupBy(job => job.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().DependsOn
                    .Where(dependency => !string.IsNullOrWhiteSpace(dependency))
                    .Select(dependency => dependency.Trim())
                    .Where(dependency => jobs.Any(job => string.Equals(job.Id?.Trim(), dependency, StringComparison.OrdinalIgnoreCase)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var cycles = new List<string>();
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = new List<string>();

        foreach (var id in graph.Keys)
        {
            Visit(id);
        }

        return cycles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        void Visit(string id)
        {
            if (visited.Contains(id))
            {
                return;
            }

            if (visiting.Contains(id))
            {
                var cycleStart = path.FindIndex(item => string.Equals(item, id, StringComparison.OrdinalIgnoreCase));
                if (cycleStart >= 0)
                {
                    var cycle = path.Skip(cycleStart).Append(id);
                    cycles.Add(string.Join(" -> ", cycle));
                }

                return;
            }

            visiting.Add(id);
            path.Add(id);

            foreach (var dependency in graph.GetValueOrDefault(id) ?? [])
            {
                Visit(dependency);
            }

            path.RemoveAt(path.Count - 1);
            visiting.Remove(id);
            visited.Add(id);
        }
    }

    private static void ValidateStatusFiles(List<ValidationResult> results, IReadOnlyList<JobConfig> jobs)
    {
        foreach (var job in jobs.Where(job => !string.IsNullOrWhiteSpace(job.StatusPath)))
        {
            var path = job.StatusPath.Trim();
            if (!File.Exists(path))
            {
                Add(results, ValidationStatus.Warning, $"Status file exists: {DisplayJob(job)}", $"Status file is missing at {path}.");
                continue;
            }

            Add(results, ValidationStatus.Pass, $"Status file exists: {DisplayJob(job)}", $"Status file was found at {path}.");

            string content;
            try
            {
                content = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                Add(results, ValidationStatus.Error, $"Status file can be read: {DisplayJob(job)}", $"Could not read {path}: {ex.Message}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                Add(results, ValidationStatus.Warning, $"Status file content parses: {DisplayJob(job)}", "Status file exists but is empty.");
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(content);
                Add(results, ValidationStatus.Pass, $"Status file content parses: {DisplayJob(job)}", "Status file content is valid JSON.");
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    Add(results, ValidationStatus.Error, $"Status file schema: {DisplayJob(job)}", "Status file root must be a JSON object.");
                    continue;
                }

                ValidateStatusJsonSchema(results, job, document.RootElement);
                ValidateLastRun(results, job, document.RootElement);
            }
            catch (JsonException ex)
            {
                Add(results, ValidationStatus.Error, $"Status file content parses: {DisplayJob(job)}", $"Status file contains invalid JSON: {ex.Message}");
            }
        }
    }

    private static void ValidateStatusJsonSchema(List<ValidationResult> results, JobConfig job, JsonElement root)
    {
        ValidateStatusValue(results, job, root);
        ValidateOptionalNonNegativeInteger(results, job, root, "warnings", ValidationStatus.Warning);
        ValidateOptionalNonNegativeInteger(results, job, root, "errors", ValidationStatus.Error);
        ValidateOptionalString(results, job, root, "runtime");
        ValidateOptionalString(results, job, root, "message");
    }

    private static void ValidateStatusValue(List<ValidationResult> results, JobConfig job, JsonElement root)
    {
        var checkName = $"status value is valid: {DisplayJob(job)}";
        if (!root.TryGetProperty("status", out var status) || status.ValueKind == JsonValueKind.Null)
        {
            Add(results, ValidationStatus.Error, checkName, "status is required but is not present.");
            return;
        }

        if (status.ValueKind != JsonValueKind.String)
        {
            Add(results, ValidationStatus.Error, checkName, "status is present but is not a string.");
            return;
        }

        var value = status.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(results, ValidationStatus.Warning, checkName, "status is present but blank; dashboard evaluation treats blank status as unknown.");
            return;
        }

        var allowedStatuses = new[] { "success", "warning", "error", "unknown" };
        if (allowedStatuses.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            Add(results, ValidationStatus.Pass, checkName, $"status is an allowed value: {value}.");
            return;
        }

        Add(results, ValidationStatus.Error, checkName, $"status has unsupported value '{value}'. Expected success, warning, error, unknown, or blank.");
    }

    private static void ValidateOptionalNonNegativeInteger(
        List<ValidationResult> results,
        JobConfig job,
        JsonElement root,
        string fieldName,
        ValidationStatus nonZeroStatus)
    {
        var typeCheckName = $"{fieldName} is numeric: {DisplayJob(job)}";
        if (!root.TryGetProperty(fieldName, out var field) || field.ValueKind == JsonValueKind.Null)
        {
            Add(results, ValidationStatus.Pass, typeCheckName, $"{fieldName} is not present.");
            return;
        }

        if (field.ValueKind != JsonValueKind.Number)
        {
            Add(results, ValidationStatus.Error, typeCheckName, $"{fieldName} is present but is not a number.");
            return;
        }

        var rawValue = field.GetRawText();
        Add(results, ValidationStatus.Pass, typeCheckName, $"{fieldName} is numeric: {rawValue}.");

        if (!field.TryGetInt32(out var count))
        {
            Add(results, ValidationStatus.Error, $"{fieldName} reported: {DisplayJob(job)}", $"{fieldName} value must be a valid integer that matches the dashboard schema: {rawValue}.");
            return;
        }

        if (count < 0)
        {
            Add(results, ValidationStatus.Error, $"{fieldName} reported: {DisplayJob(job)}", $"{fieldName} value must be non-negative: {rawValue}.");
            return;
        }

        if (count > 0)
        {
            Add(results, nonZeroStatus, $"{fieldName} reported: {DisplayJob(job)}", $"{DisplayJob(job)} reports {count} {fieldName}.");
            return;
        }

        Add(results, ValidationStatus.Pass, $"{fieldName} reported: {DisplayJob(job)}", $"{DisplayJob(job)} reports no {fieldName}.");
    }

    private static void ValidateOptionalString(List<ValidationResult> results, JobConfig job, JsonElement root, string fieldName)
    {
        var checkName = $"{fieldName} is a string: {DisplayJob(job)}";
        if (!root.TryGetProperty(fieldName, out var field) || field.ValueKind == JsonValueKind.Null)
        {
            Add(results, ValidationStatus.Pass, checkName, $"{fieldName} is not present.");
            return;
        }

        if (field.ValueKind == JsonValueKind.String)
        {
            Add(results, ValidationStatus.Pass, checkName, $"{fieldName} is a string.");
            return;
        }

        Add(results, ValidationStatus.Error, checkName, $"{fieldName} is present but is not a string.");
    }

    private static void ValidateLastRun(List<ValidationResult> results, JobConfig job, JsonElement root)
    {
        if (!root.TryGetProperty("last_run", out var lastRun) || lastRun.ValueKind == JsonValueKind.Null)
        {
            Add(results, ValidationStatus.Pass, $"last_run parses: {DisplayJob(job)}", "last_run is not present.");
            return;
        }

        if (lastRun.ValueKind != JsonValueKind.String)
        {
            Add(results, ValidationStatus.Error, $"last_run parses: {DisplayJob(job)}", "last_run is present but is not a string.");
            return;
        }

        var value = lastRun.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(results, ValidationStatus.Warning, $"last_run parses: {DisplayJob(job)}", "last_run is present but empty.");
            return;
        }

        if (DateTimeOffset.TryParse(value, out _))
        {
            Add(results, ValidationStatus.Pass, $"last_run parses: {DisplayJob(job)}", $"last_run parsed successfully: {value}.");
            return;
        }

        Add(results, ValidationStatus.Error, $"last_run parses: {DisplayJob(job)}", $"last_run could not be parsed: {value}.");
    }

    private static void AddFieldValidation(
        List<ValidationResult> results,
        IReadOnlyList<JobConfig> jobs,
        string checkName,
        Func<JobConfig, string> selector,
        string fieldName,
        string passDetails)
    {
        var invalidJobs = jobs
            .Where(job => string.IsNullOrWhiteSpace(selector(job)))
            .Select(DisplayJob)
            .ToList();

        if (invalidJobs.Count > 0)
        {
            Add(results, ValidationStatus.Error, checkName, $"Empty {fieldName} for: {string.Join(", ", invalidJobs)}.");
        }
        else
        {
            Add(results, ValidationStatus.Pass, checkName, passDetails);
        }
    }

    private static void Add(List<ValidationResult> results, ValidationStatus status, string checkName, string details)
    {
        results.Add(new ValidationResult
        {
            Status = status,
            CheckName = checkName,
            Details = details
        });
    }

    private static string DisplayJob(JobConfig job)
    {
        return string.IsNullOrWhiteSpace(job.Id) ? "unnamed job" : job.Id.Trim();
    }
}
