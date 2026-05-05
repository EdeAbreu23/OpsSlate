namespace HomelabAutomationCenter.Models;

public sealed class JobStatus
{
    public string? Status { get; set; }
    public DateTimeOffset? LastRun { get; set; }
    public string? Runtime { get; set; }
    public string? Message { get; set; }
    public int Warnings { get; set; }
    public int Errors { get; set; }
}
