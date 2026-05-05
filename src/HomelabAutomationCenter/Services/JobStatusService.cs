using System.Text.Json;
using HomelabAutomationCenter.Models;

namespace HomelabAutomationCenter.Services;

public sealed class JobStatusService
{
    public (bool fileFound, bool isValidJson, JobStatus? status) ReadStatus(string statusPath)
    {
        if (string.IsNullOrWhiteSpace(statusPath) || !File.Exists(statusPath))
        {
            return (false, false, null);
        }

        try
        {
            var json = File.ReadAllText(statusPath);
            var status = JsonSerializer.Deserialize<JobStatus>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return status is null
                ? (true, false, null)
                : (true, true, status);
        }
        catch
        {
            return (true, false, null);
        }
    }
}
