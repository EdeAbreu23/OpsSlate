namespace OpsSlate.Models;

public enum ValidationStatus
{
    Pass,
    Warning,
    Error
}

public sealed class ValidationResult
{
    public ValidationStatus Status { get; init; }
    public string CheckName { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
}
