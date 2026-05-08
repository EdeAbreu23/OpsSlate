using HomelabAutomationCenter.Models;

namespace HomelabAutomationCenter.Services;

public sealed class SystemInfoService
{
    private const string ConfigDirectory = "/config";
    private const string JobsConfigPath = "/config/jobs.yml";
    private const string StatusDirectory = "/status";

    private readonly JobConfigService _jobConfigService;

    public SystemInfoService(JobConfigService jobConfigService)
    {
        _jobConfigService = jobConfigService;
    }

    public SystemInfo GetInfo()
    {
        return new SystemInfo
        {
            ConfigPath = JobsConfigPath,
            ConfigDirectoryExists = Directory.Exists(ConfigDirectory),
            JobsConfigExists = File.Exists(JobsConfigPath),
            StatusDirectoryExists = Directory.Exists(StatusDirectory),
            ConfiguredJobCount = _jobConfigService.ReadJobs().Count,
            StatusFileCount = CountStatusFiles()
        };
    }

    private static int CountStatusFiles()
    {
        if (!Directory.Exists(StatusDirectory))
        {
            return 0;
        }

        try
        {
            return Directory.EnumerateFiles(StatusDirectory, "*", SearchOption.AllDirectories).Count();
        }
        catch
        {
            return 0;
        }
    }
}
