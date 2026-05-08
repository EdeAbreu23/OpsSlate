namespace HomelabAutomationCenter.Models;

public sealed class JobStatusReadResult
{
    public bool FileFound { get; init; }
    public bool IsValidJson { get; init; }
    public JobStatus? Status { get; init; }
    public string? ErrorMessage { get; init; }
}
