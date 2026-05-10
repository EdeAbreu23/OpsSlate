using OpsSlate.Models;
using OpsSlate.Options;
using Microsoft.Extensions.Options;

namespace OpsSlate.Services;

public sealed class JobDashboardService
{
    private readonly JobConfigService _jobConfigService;
    private readonly JobStatusService _jobStatusService;
    private readonly JobHealthEvaluator _jobHealthEvaluator;
    private readonly OpsSlatePathOptions _pathOptions;

    public JobDashboardService(
        JobConfigService jobConfigService,
        JobStatusService jobStatusService,
        JobHealthEvaluator jobHealthEvaluator,
        IOptions<OpsSlatePathOptions> pathOptions)
    {
        _jobConfigService = jobConfigService;
        _jobStatusService = jobStatusService;
        _jobHealthEvaluator = jobHealthEvaluator;
        _pathOptions = pathOptions.Value;
    }

    public IReadOnlyList<JobViewModel> GetJobs()
    {
        var jobs = _jobConfigService.ReadJobs();
        var evaluatedJobs = jobs
            .Select(job =>
            {
                var read = ReadStatus(job);
                return _jobHealthEvaluator.Evaluate(job, read);
            })
            .ToList();

        ApplyDependencyBlocks(evaluatedJobs, jobs);

        return evaluatedJobs
            .OrderBy(j => StatusSortOrder(j.FinalStatus))
            .ThenBy(j => j.Name)
            .ToList();
    }

    public JobViewModel? GetJob(string id)
    {
        return GetJobs().FirstOrDefault(j => string.Equals(j.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private JobStatusReadResult ReadStatus(JobConfig job)
    {
        if (!_pathOptions.TryResolveStatusPath(job.StatusPath, out var resolvedStatusPath, out var pathError))
        {
            return new JobStatusReadResult
            {
                FileFound = false,
                IsValidJson = false,
                ErrorMessage = pathError ?? "status_path must resolve under the configured status root."
            };
        }

        return _jobStatusService.ReadStatus(resolvedStatusPath);
    }

    public static int StatusSortOrder(string finalStatus)
    {
        return finalStatus switch
        {
            JobFinalStatus.Error => 0,
            JobFinalStatus.Blocked => 1,
            JobFinalStatus.Stale => 2,
            JobFinalStatus.Warning => 3,
            JobFinalStatus.Success => 4,
            _ => 5
        };
    }

    private static void ApplyDependencyBlocks(List<JobViewModel> evaluatedJobs, IReadOnlyList<JobConfig> jobs)
    {
        var configsById = jobs
            .GroupBy(j => j.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var statusesById = evaluatedJobs
            .GroupBy(j => j.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Safety limit prevents cyclic dependency relationships from causing endless re-evaluation.
        var maxIterations = Math.Max(evaluatedJobs.Count * 2, 1);
        var iterations = 0;
        var changed = true;
        while (changed && iterations < maxIterations)
        {
            iterations++;
            changed = false;

            for (var i = 0; i < evaluatedJobs.Count; i++)
            {
                var job = evaluatedJobs[i];
                if (!configsById.TryGetValue(job.Id, out var config) || config.DependsOn.Count == 0)
                {
                    continue;
                }

                var blockers = config.DependsOn
                    .Where(dependencyId => !statusesById.TryGetValue(dependencyId, out var dependency)
                        || dependency.FinalStatus != JobFinalStatus.Success)
                    .ToList();

                if (blockers.Count == 0)
                {
                    continue;
                }

                var blockedReason = $"Blocked by {string.Join(", ", blockers)}";
                if (job.FinalStatus == JobFinalStatus.Blocked && job.Reason == blockedReason)
                {
                    continue;
                }

                var blockedJob = Blocked(job, blockedReason);
                evaluatedJobs[i] = blockedJob;
                statusesById[blockedJob.Id] = blockedJob;
                changed = true;
            }
        }
    }

    private static JobViewModel Blocked(JobViewModel job, string reason)
    {
        return new JobViewModel
        {
            Id = job.Id,
            Name = job.Name,
            FinalStatus = JobFinalStatus.Blocked,
            Reason = reason,
            RawStatus = job.RawStatus,
            LastRun = job.LastRun,
            LastRunAge = job.LastRunAge,
            Runtime = job.Runtime,
            Message = job.Message,
            Warnings = job.Warnings,
            Errors = job.Errors,
            IsStale = job.IsStale,
            FileFound = job.FileFound,
            StatusPath = job.StatusPath,
            StatusReadErrorMessage = job.StatusReadErrorMessage,
            DependsOn = job.DependsOn
        };
    }
}
