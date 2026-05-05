using HomelabAutomationCenter.Models;

namespace HomelabAutomationCenter.Services;

public sealed class JobHealthEvaluator
{
    public JobViewModel Evaluate(JobConfig job, bool fileFound, bool isValidJson, JobStatus? status)
    {
        if (!fileFound || !isValidJson || status is null)
        {
            var reason = fileFound ? "Status file invalid" : "Status file missing";
            return Base(job, "UNKNOWN", reason, "unknown", false, fileFound, status);
        }

        var rawStatus = (status.Status ?? "unknown").Trim().ToLowerInvariant();
        var isStale = IsStale(status.LastRun, job.StaleAfterMinutes);

        var finalStatus = DetermineFinal(rawStatus, status.Errors, status.Warnings, isStale);
        var reason = DetermineReason(finalStatus, status.Errors, status.Warnings);

        return Base(job, finalStatus, reason, rawStatus, isStale, fileFound, status);
    }

    private static string DetermineFinal(string rawStatus, int errors, int warnings, bool isStale)
    {
        if (errors > 0 || rawStatus == "error") return "ERROR";
        if (isStale) return "STALE";
        if (warnings > 0 || rawStatus == "warning") return "WARNING";
        return "SUCCESS";
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

    private static string DetermineReason(string finalStatus, int errors, int warnings)
    {
        return finalStatus switch
        {
            "ERROR" => errors == 1 ? "1 error reported" : $"{errors} errors reported",
            "STALE" => "Last run is stale",
            "WARNING" => warnings == 1 ? "1 warning reported" : $"{warnings} warnings reported",
            "SUCCESS" => "Job completed successfully",
            _ => "Status file invalid"
        };
    }

    private static JobViewModel Base(JobConfig job, string finalStatus, string reason, string rawStatus, bool isStale, bool fileFound, JobStatus? status)
    {
        return new JobViewModel
        {
            Id = job.Id,
            Name = job.Name,
            FinalStatus = finalStatus,
            Reason = reason,
            RawStatus = rawStatus,
            LastRun = status?.LastRun,
            Runtime = status?.Runtime ?? "-",
            Message = status?.Message ?? "",
            Warnings = status?.Warnings ?? 0,
            Errors = status?.Errors ?? 0,
            IsStale = isStale,
            FileFound = fileFound
        };
    }
}
