using OpsSlate.Models;
using OpsSlate.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OpsSlate.Pages;

public sealed class IndexModel : PageModel
{
    private readonly JobDashboardService _jobDashboardService;

    public IndexModel(JobDashboardService jobDashboardService)
    {
        _jobDashboardService = jobDashboardService;
    }

    public IReadOnlyList<JobViewModel> Jobs { get; private set; } = [];
    public int AutoRefreshSeconds { get; } = 60;

    public void OnGet()
    {
        Jobs = _jobDashboardService.GetJobs();
    }
}
