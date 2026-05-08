namespace HomelabAutomationCenter.Models;

public sealed class SystemInfo
{
    public string ConfigPath { get; set; } = string.Empty;
    public bool ConfigDirectoryExists { get; set; }
    public bool JobsConfigExists { get; set; }
    public bool StatusDirectoryExists { get; set; }
    public int ConfiguredJobCount { get; set; }
    public int StatusFileCount { get; set; }
}
