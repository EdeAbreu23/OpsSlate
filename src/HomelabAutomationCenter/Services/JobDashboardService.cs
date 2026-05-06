using HomelabAutomationCenter.Models;

namespace HomelabAutomationCenter.Services;

public sealed class JobDashboardService
{
    private readonly JobConfigService _jobConfigService;
    private readonly JobStatusService _jobStatusService;
    private readonly JobHealthEvaluator _jobHealthEvaluator;

    public JobDashboardService(
        JobConfigService jobConfigService,
        JobStatusService jobStatusService,
        JobHealthEvaluator jobHealthEvaluator)
    {
        _jobConfigService = jobConfigService;
        _jobStatusService = jobStatusService;
        _jobHealthEvaluator = jobHealthEvaluator;
    }

    public IReadOnlyList<JobViewModel> GetJobs()
    {
        var jobs = _jobConfigService.ReadJobs();
        var evaluatedJobs = jobs
            .Select(job =>
            {
                var read = _jobStatusService.ReadStatus(job.StatusPath);
                return _jobHealthEvaluator.Evaluate(job, read.fileFound, read.isValidJson, read.status);
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

    public static int StatusSortOrder(string finalStatus)
    {
        return finalStatus switch
        {
            "ERROR" => 0,
            "BLOCKED" => 1,
            "STALE" => 2,
            "WARNING" => 3,
            "SUCCESS" => 4,
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
                        || dependency.FinalStatus != "SUCCESS")
                    .ToList();

                if (blockers.Count == 0)
                {
                    continue;
                }

                var blockedReason = $"Blocked by {string.Join(", ", blockers)}";
                if (job.FinalStatus == "BLOCKED" && job.Reason == blockedReason)
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
            FinalStatus = "BLOCKED",
            Reason = reason,
            RawStatus = job.RawStatus,
            LastRun = job.LastRun,
            Runtime = job.Runtime,
            Message = job.Message,
            Warnings = job.Warnings,
            Errors = job.Errors,
            IsStale = job.IsStale,
            FileFound = job.FileFound,
            StatusPath = job.StatusPath,
            DependsOn = job.DependsOn
        };
    }
}
