using OpsSlate.Options;
using Xunit;

namespace OpsSlate.Tests;

public sealed class OpsSlatePathOptionsTests
{
    [Fact]
    public void ResolveStatusPath_AllowsRelativePathUnderStatusRoot()
    {
        var options = new OpsSlatePathOptions
        {
            StatusRoot = "/status"
        };

        var resolvedPath = options.ResolveStatusPath("backup/status.json");

        Assert.Equal(Path.GetFullPath("/status/backup/status.json"), resolvedPath);
    }

    [Fact]
    public void ResolveStatusPath_AllowsStatusRootItself()
    {
        var options = new OpsSlatePathOptions
        {
            StatusRoot = "/status"
        };

        var resolvedPath = options.ResolveStatusPath("/status");

        Assert.Equal(Path.GetFullPath("/status"), resolvedPath);
    }

    [Theory]
    [InlineData("../outside/status.json")]
    [InlineData("/etc/passwd")]
    [InlineData("/status-other/job.json")]
    public void ResolveStatusPath_RejectsPathsOutsideStatusRoot(string statusPath)
    {
        var options = new OpsSlatePathOptions
        {
            StatusRoot = "/status"
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.ResolveStatusPath(statusPath));

        Assert.Contains("status_path must resolve under", exception.Message);
    }
}
