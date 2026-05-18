using System.Text.Json;
using OpsSlate.Models;

namespace OpsSlate.Services;

public sealed class JobStatusService
{
    public JobStatusReadResult ReadStatus(string statusPath)
    {
        if (string.IsNullOrWhiteSpace(statusPath))
        {
            return Invalid(false, "status_path is empty.");
        }

        if (!File.Exists(statusPath))
        {
            return Invalid(false, "Status file was not found.");
        }

        string json;
        try
        {
            json = File.ReadAllText(statusPath);
        }
        catch (Exception)
        {
            return Invalid(true, "Could not read status file.");
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return Invalid(true, "Status file is empty.");
        }

        try
        {
            var status = JsonSerializer.Deserialize<JobStatus>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return status is null
                ? Invalid(true, "Status file did not contain a JSON object.")
                : new JobStatusReadResult
                {
                    FileFound = true,
                    IsValidJson = true,
                    Status = status
                };
        }
        catch (JsonException ex)
        {
            return Invalid(true, $"Status file contains invalid JSON: {Concise(ex.Message)}");
        }
        catch (NotSupportedException ex)
        {
            return Invalid(true, $"Status file could not be deserialized: {Concise(ex.Message)}");
        }
        catch (Exception ex)
        {
            return Invalid(true, $"Status file could not be processed: {Concise(ex.Message)}");
        }
    }

    private static string Concise(string message)
    {
        const int maxDisplayLength = 240;

        var singleLineMessage = string.Join(" ", message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        return singleLineMessage.Length <= maxDisplayLength
            ? singleLineMessage
            : string.Concat(singleLineMessage.AsSpan(0, maxDisplayLength), "...");
    }

    private static JobStatusReadResult Invalid(bool fileFound, string errorMessage)
    {
        return new JobStatusReadResult
        {
            FileFound = fileFound,
            IsValidJson = false,
            ErrorMessage = errorMessage
        };
    }
}
