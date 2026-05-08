using HomelabAutomationCenter.Models;

namespace HomelabAutomationCenter.Services;

public sealed class JobHealthEvaluator
{
    private readonly TimeFormatter _timeFormatter;

    public JobHealthEvaluator(TimeFormatter timeFormatter)
    {
        _timeFormatter = timeFormatter;
    }

    public JobViewModel Evaluate(JobConfig job, JobStatusReadResult readResult)
    {
        if (!readResult.FileFound || !readResult.IsValidJson || readResult.Status is null)
        {
            var unknownReason = readResult.FileFound ? "Status file invalid" : "Status file missing";
            return Base(
                job,
                JobFinalStatus.Unknown,
                unknownReason,
                "unknown",
                false,
                readResult.FileFound,
                readResult.Status,
                readResult.ErrorMessage);
        }

        var status = readResult.Status;
        var rawStatus = (status.Status ?? "unknown").Trim().ToLowerInvariant();
        var isStale = IsStale(status.LastRun, job.StaleAfterMinutes);

        var finalStatus = DetermineFinal(rawStatus, status.Errors, status.Warnings, isStale);
        var reason = DetermineReason(finalStatus, status.Errors, status.Warnings, status.LastRun, job.StaleAfterMinutes);

        return Base(
            job,
            finalStatus,
            reason,
            rawStatus,
            isStale,
            readResult.FileFound,
            status,
            readResult.ErrorMessage);
    }

    private static string DetermineFinal(string rawStatus, int errors, int warnings, bool isStale)
    {
        if (errors > 0 || rawStatus == "error") return JobFinalStatus.Error;
        if (isStale) return JobFinalStatus.Stale;
        if (warnings > 0 || rawStatus == "warning") return JobFinalStatus.Warning;
        return JobFinalStatus.Success;
    }

    private static bool IsStale(DateTimeOffset? lastRun, int staleAfterMinutes)
    {
        if (lastRun is null)
        {
            return true;
        }

        var threshold = DateTimeOffset.UtcNow.AddMinutes(-staleAfterMinutes);
        return lastRun.Value < threshold;
    }

    private string DetermineReason(string finalStatus, int errors, int warnings, DateTimeOffset? lastRun, int staleAfterMinutes)
    {
        return finalStatus switch
        {
            JobFinalStatus.Error => errors switch
            {
                1 => "1 error reported",
                > 1 => $"{errors} errors reported",
                _ => "Error reported"
            },
            JobFinalStatus.Stale => _timeFormatter.FormatStaleReason(lastRun, staleAfterMinutes),
            JobFinalStatus.Warning => warnings switch
            {
                1 => "1 warning reported",
                > 1 => $"{warnings} warnings reported",
                _ => "Warning reported"
            },
            JobFinalStatus.Success => "Job completed successfully",
            _ => "Status file invalid"
        };
    }

    private JobViewModel Base(
        JobConfig job,
        string finalStatus,
        string reason,
        string rawStatus,
        bool isStale,
        bool fileFound,
        JobStatus? status,
        string? statusReadErrorMessage)
    {
        return new JobViewModel
        {
            Id = job.Id,
            Name = job.Name,
            FinalStatus = finalStatus,
            Reason = reason,
            RawStatus = rawStatus,
            LastRun = status?.LastRun,
            LastRunAge = _timeFormatter.FormatLastRunAge(status?.LastRun),
            Runtime = status?.Runtime ?? "-",
            Message = status?.Message ?? "",
            Warnings = status?.Warnings ?? 0,
            Errors = status?.Errors ?? 0,
            IsStale = isStale,
            FileFound = fileFound,
            StatusPath = job.StatusPath,
            StatusReadErrorMessage = statusReadErrorMessage ?? string.Empty,
            DependsOn = job.DependsOn
        };
    }
}