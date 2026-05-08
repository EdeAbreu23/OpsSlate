using System.Text.Json.Serialization;
using OpsSlate.Serialization;

namespace OpsSlate.Models;

public sealed class JobStatus
{
    public string? Status { get; set; }

    [JsonPropertyName("last_run")]
    [JsonConverter(typeof(NullableDateTimeOffsetConverter))]
    public DateTimeOffset? LastRun { get; set; }

    public string? Runtime { get; set; }
    public string? Message { get; set; }
    public int Warnings { get; set; }
    public int Errors { get; set; }
}
