namespace OpsSlate.Models;

public sealed class JobViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string FinalStatus { get; init; } = JobFinalStatus.Unknown;
    public string Reason { get; init; } = "Status file missing";
    public string RawStatus { get; init; } = "unknown";
    public DateTimeOffset? LastRun { get; init; }
    public string LastRunAge { get; init; } = "Never";
    public string Runtime { get; init; } = "-";
    public string Message { get; init; } = "";
    public int Warnings { get; init; }
    public int Errors { get; init; }
    public bool IsStale { get; init; }
    public bool FileFound { get; init; }
    public string StatusPath { get; init; } = string.Empty;
    public string StatusReadErrorMessage { get; init; } = string.Empty;
    public IReadOnlyList<string> DependsOn { get; init; } = [];
}
