namespace OpsSlate.Tests;

internal sealed class TestTempDirectory : IDisposable
{
    private TestTempDirectory(string root)
    {
        Root = root;
        ConfigDirectory = Path.Combine(root, "config");
        ConfigPath = Path.Combine(ConfigDirectory, "jobs.yml");
        StatusRoot = Path.Combine(root, "status");
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(StatusRoot);
    }

    public string Root { get; }
    public string ConfigDirectory { get; }
    public string ConfigPath { get; }
    public string StatusRoot { get; }

    public static TestTempDirectory Create()
    {
        return new TestTempDirectory(Path.Combine(Path.GetTempPath(), "OpsSlate.Tests", Guid.NewGuid().ToString("N")));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup for test temp directories.
        }
    }
}
