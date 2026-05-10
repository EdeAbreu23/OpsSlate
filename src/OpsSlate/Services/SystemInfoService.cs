using OpsSlate.Models;
using OpsSlate.Options;
using Microsoft.Extensions.Options;

namespace OpsSlate.Services;

public sealed class SystemInfoService
{
    private readonly JobConfigService _jobConfigService;
    private readonly OpsSlatePathOptions _pathOptions;

    public SystemInfoService(JobConfigService jobConfigService, IOptions<OpsSlatePathOptions> pathOptions)
    {
        _jobConfigService = jobConfigService;
        _pathOptions = pathOptions.Value;
    }

    public SystemInfo GetInfo()
    {
        var configDirectory = Path.GetDirectoryName(_pathOptions.ConfigPath);
        return new SystemInfo
        {
            ConfigPathConfigured = !string.IsNullOrWhiteSpace(_pathOptions.ConfigPath),
            ConfigDirectoryExists = !string.IsNullOrWhiteSpace(configDirectory) && Directory.Exists(configDirectory),
            JobsConfigExists = File.Exists(_pathOptions.ConfigPath),
            StatusDirectoryExists = Directory.Exists(_pathOptions.StatusRoot),
            ConfiguredJobCount = _jobConfigService.ReadJobs().Count,
            StatusFileCount = CountStatusFiles()
        };
    }

    private int CountStatusFiles()
    {
        if (!Directory.Exists(_pathOptions.StatusRoot))
        {
            return 0;
        }

        try
        {
            return Directory.EnumerateFiles(_pathOptions.StatusRoot, "*", SearchOption.AllDirectories).Count();
        }
        catch
        {
            return 0;
        }
    }
}
