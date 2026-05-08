namespace OpsSlate.Models;

public sealed class JobConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string StatusPath { get; set; } = string.Empty;
    public int StaleAfterMinutes { get; set; } = 60;
    public List<string> DependsOn { get; set; } = [];
}
