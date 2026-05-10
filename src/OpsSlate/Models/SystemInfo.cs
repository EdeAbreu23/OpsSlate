namespace OpsSlate.Models;

public sealed class SystemInfo
{
    public bool ConfigPathConfigured { get; set; }
    public bool ConfigDirectoryExists { get; set; }
    public bool JobsConfigExists { get; set; }
    public bool StatusDirectoryExists { get; set; }
    public int ConfiguredJobCount { get; set; }
    public int StatusFileCount { get; set; }
}
