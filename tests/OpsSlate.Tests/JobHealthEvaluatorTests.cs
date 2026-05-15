using OpsSlate.Models;
using OpsSlate.Services;
using Xunit;

namespace OpsSlate.Tests;

public sealed class JobHealthEvaluatorTests
{
    private readonly JobHealthEvaluator _evaluator = new(new TimeFormatter());

    [Fact]
    public void Evaluate_ReturnsUnknownWhenStatusFileIsMissing()
    {
        var result = _evaluator.Evaluate(Job(), new JobStatusReadResult
        {
            FileFound = false,
            IsValidJson = false,
            ErrorMessage = "Status file was not found."
        });

        Assert.Equal(JobFinalStatus.Unknown, result.FinalStatus);
        Assert.Equal("Status file missing", result.Reason);
        Assert.False(result.FileFound);
    }

    [Fact]
    public void Evaluate_ErrorTakesPrecedenceOverStaleAndWarnings()
    {
        var result = _evaluator.Evaluate(Job(), FoundStatus(new JobStatus
        {
            Status = "warning",
            LastRun = DateTimeOffset.UtcNow.AddDays(-2),
            Warnings = 3,
            Errors = 1
        }));

        Assert.Equal(JobFinalStatus.Error, result.FinalStatus);
        Assert.Equal("1 error reported", result.Reason);
        Assert.True(result.IsStale);
    }

    [Fact]
    public void Evaluate_StaleTakesPrecedenceOverWarnings()
    {
        var result = _evaluator.Evaluate(Job(), FoundStatus(new JobStatus
        {
            Status = "success",
            LastRun = DateTimeOffset.UtcNow.AddDays(-2),
            Warnings = 2
        }));

        Assert.Equal(JobFinalStatus.Stale, result.FinalStatus);
        Assert.True(result.IsStale);
    }

    [Fact]
    public void Evaluate_WarningIsReturnedWhenStatusHasWarnings()
    {
        var result = _evaluator.Evaluate(Job(), FoundStatus(new JobStatus
        {
            Status = "success",
            LastRun = DateTimeOffset.UtcNow,
            Warnings = 2
        }));

        Assert.Equal(JobFinalStatus.Warning, result.FinalStatus);
        Assert.Equal("2 warnings reported", result.Reason);
    }

    [Fact]
    public void Evaluate_SuccessIsReturnedForFreshStatusWithoutWarningsOrErrors()
    {
        var result = _evaluator.Evaluate(Job(), FoundStatus(new JobStatus
        {
            Status = "success",
            LastRun = DateTimeOffset.UtcNow,
            Runtime = "00:00:03",
            Message = "Completed."
        }));

        Assert.Equal(JobFinalStatus.Success, result.FinalStatus);
        Assert.Equal("Job completed successfully", result.Reason);
        Assert.False(result.IsStale);
    }

    private static JobConfig Job() => new()
    {
        Id = "backup",
        Name = "Backup",
        StatusPath = "backup/status.json",
        StaleAfterMinutes = 60
    };

    private static JobStatusReadResult FoundStatus(JobStatus status) => new()
    {
        FileFound = true,
        IsValidJson = true,
        Status = status
    };
}
