namespace HomelabAutomationCenter.Services;

public sealed class TimeFormatter
{
    public string FormatLastRunAge(DateTimeOffset? lastRun)
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

    public string FormatStaleReason(DateTimeOffset? lastRun, int staleAfterMinutes)
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

        return $"Last run {FormatHoursAndMinutes(elapsed)} ago (threshold {staleAfterMinutes}m)";
    }

    private static string FormatHoursAndMinutes(TimeSpan elapsed)
    {
        var totalHours = (int)elapsed.TotalHours;
        var minutes = elapsed.Minutes;

        return totalHours > 0
            ? $"{totalHours}h {minutes}m"
            : $"{minutes}m";
    }
}
