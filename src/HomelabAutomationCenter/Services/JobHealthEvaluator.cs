using HomelabAutomationCenter.Models;

namespace HomelabAutomationCenter.Services;

public sealed class JobHealthEvaluator
{
    public JobViewModel Evaluate(JobConfig job, JobStatusReadResult readResult)
    {
        if (!readResult.FileFound || !readResult.IsValidJson || readResult.Status is null)
        {
            var unknownReason = readResult.FileFound ? "Status file invalid" : "Status file missing";
            return Base(job, "UNKNOWN", unknownReason, "unknown", false, readResult.FileFound, readResult.Status, readResult.ErrorMessage);
        }

        var status = readResult.Status;

        var rawStatus = (status.Status ?? "unknown").Trim().ToLowerInvariant();
        var isStale = IsStale(status.LastRun, job.StaleAfterMinutes);

        var finalStatus = DetermineFinal(rawStatus, status.Errors, status.Warnings, isStale);
        var reason = DetermineReason(finalStatus, status.Errors, status.Warnings, status.LastRun, job.StaleAfterMinutes);

        return Base(job, finalStatus, reason, rawStatus, isStale, readResult.FileFound, status, readResult.ErrorMessage);
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

    private static string FormatLastRunAge(DateTimeOffset? lastRun)
    {
        if (lastRun is null)
        {
            return "Never";
        }

        var elapsed = DateTimeOffset.UtcNow - lastRun.Value;
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "Just now";
        }

        var days = (int)elapsed.TotalDays;
        if (days > 0)
        {
            return $"{days}d {elapsed.Hours}h ago";
        }

        var hours = (int)elapsed.TotalHours;
        if (hours > 0)
        {
            return $"{hours}h {elapsed.Minutes}m ago";
        }

        return $"{elapsed.Minutes}m ago";
    }

    private static JobViewModel Base(JobConfig job, string finalStatus, string reason, string rawStatus, bool isStale, bool fileFound, JobStatus? status, string? statusReadErrorMessage)
    {
        return new JobViewModel
        {
            Id = job.Id,
            Name = job.Name,
            FinalStatus = finalStatus,
            Reason = reason,
            RawStatus = rawStatus,
            LastRun = status?.LastRun,
            LastRunAge = FormatLastRunAge(status?.LastRun),
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
