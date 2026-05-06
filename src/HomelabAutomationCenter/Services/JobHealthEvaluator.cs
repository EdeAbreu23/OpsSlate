using HomelabAutomationCenter.Models;

namespace HomelabAutomationCenter.Services;

public sealed class JobHealthEvaluator
{
    public JobViewModel Evaluate(JobConfig job, bool fileFound, bool isValidJson, JobStatus? status)
    {
        if (!fileFound || !isValidJson || status is null)
        {
            var unknownReason = fileFound ? "Status file invalid" : "Status file missing";
            return Base(job, "UNKNOWN", unknownReason, "unknown", false, fileFound, status);
        }

        var rawStatus = (status.Status ?? "unknown").Trim().ToLowerInvariant();
        var isStale = IsStale(status.LastRun, job.StaleAfterMinutes);

        var finalStatus = DetermineFinal(rawStatus, status.Errors, status.Warnings, isStale);
        var reason = DetermineReason(finalStatus, status.Errors, status.Warnings, status.LastRun, job.StaleAfterMinutes);

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

    private static string DetermineReason(string finalStatus, int errors, int warnings, DateTimeOffset? lastRun, int staleAfterMinutes)
    {
        return finalStatus switch
        {
            "ERROR" => errors switch
            {
                1 => "1 error reported",
                > 1 => $"{errors} errors reported",
                _ => "Error reported"
            },
            "STALE" => FormatStaleReason(lastRun, staleAfterMinutes),
            "WARNING" => warnings switch
            {
                1 => "1 warning reported",
                > 1 => $"{warnings} warnings reported",
                _ => "Warning reported"
            },
            "SUCCESS" => "Job completed successfully",
            _ => "Status file invalid"
        };
    }

    private static string FormatStaleReason(DateTimeOffset? lastRun, int staleAfterMinutes)
    {
        if (lastRun is null)
        {
            return $"Never ran (threshold {staleAfterMinutes}m)";
        }

        var elapsed = DateTimeOffset.UtcNow - lastRun.Value;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var totalHours = (int)elapsed.TotalHours;
        var minutes = elapsed.Minutes;

        var ago = totalHours > 0
            ? $"{totalHours}h {minutes}m"
            : $"{minutes}m";

        return $"Last run {ago} ago (threshold {staleAfterMinutes}m)";
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
            FileFound = fileFound,
            StatusPath = job.StatusPath,
            DependsOn = job.DependsOn
        };
    }
}
