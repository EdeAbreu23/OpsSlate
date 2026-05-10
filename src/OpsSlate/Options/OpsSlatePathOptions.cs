namespace OpsSlate.Options;

public sealed class OpsSlatePathOptions
{
    public const string DefaultConfigPath = "/config/jobs.yml";
    public const string DefaultStatusRoot = "/status";

    public string ConfigPath { get; set; } = DefaultConfigPath;
    public string StatusRoot { get; set; } = DefaultStatusRoot;

    public string ResolveStatusPath(string statusPath)
    {
        if (string.IsNullOrWhiteSpace(statusPath))
        {
            throw new InvalidOperationException("status_path is required.");
        }

        var trimmedPath = statusPath.Trim();
        var fullStatusRoot = Path.GetFullPath(StatusRoot);
        var resolvedPath = Path.IsPathRooted(trimmedPath)
            ? Path.GetFullPath(trimmedPath)
            : Path.GetFullPath(Path.Combine(fullStatusRoot, trimmedPath));

        if (!IsPathWithinRoot(resolvedPath, fullStatusRoot))
        {
            throw new InvalidOperationException($"status_path must resolve under {DefaultStatusRoot} or the configured HAC_STATUS_ROOT directory.");
        }

        return resolvedPath;
    }

    public bool TryResolveStatusPath(string statusPath, out string resolvedPath, out string? errorMessage)
    {
        try
        {
            resolvedPath = ResolveStatusPath(statusPath);
            errorMessage = null;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or InvalidOperationException)
        {
            resolvedPath = string.Empty;
            errorMessage = ex.Message;
            return false;
        }
    }

    private static bool IsPathWithinRoot(string candidatePath, string rootPath)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(rootPath);
        return string.Equals(candidatePath, normalizedRoot, StringComparison.Ordinal)
            || candidatePath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
