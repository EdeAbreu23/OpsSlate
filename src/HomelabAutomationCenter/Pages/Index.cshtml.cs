using HomelabAutomationCenter.Models;
using HomelabAutomationCenter.Options;
using HomelabAutomationCenter.Services;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomelabAutomationCenter.Pages;

public sealed class IndexModel : PageModel
{
    private readonly JobDashboardService _jobDashboardService;
    private readonly HacPathOptions _pathOptions;

    public IndexModel(JobDashboardService jobDashboardService, IOptions<HacPathOptions> pathOptions)
    {
        _jobDashboardService = jobDashboardService;
        _pathOptions = pathOptions.Value;
    }

    public IReadOnlyList<JobViewModel> Jobs { get; private set; } = [];
    public int AutoRefreshSeconds { get; } = 60;
    public string ConfigPath => _pathOptions.ConfigPath;

    public void OnGet()
    {
        Jobs = _jobDashboardService.GetJobs();
    }
}
