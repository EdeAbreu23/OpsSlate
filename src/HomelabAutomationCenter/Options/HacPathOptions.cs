namespace HomelabAutomationCenter.Options;

public sealed class HacPathOptions
{
    public const string DefaultConfigPath = "/config/jobs.yml";
    public const string DefaultStatusRoot = "/status";

    public string ConfigPath { get; set; } = DefaultConfigPath;
    public string StatusRoot { get; set; } = DefaultStatusRoot;

    public string ResolveStatusPath(string statusPath)
    {
        var trimmedPath = statusPath.Trim();
        if (Path.IsPathRooted(trimmedPath))
        {
            return trimmedPath;
        }

        return Path.GetFullPath(Path.Combine(StatusRoot, trimmedPath));
    }
}
