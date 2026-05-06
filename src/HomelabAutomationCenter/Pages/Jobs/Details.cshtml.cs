using HomelabAutomationCenter.Models;
using HomelabAutomationCenter.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomelabAutomationCenter.Pages.Jobs;

public sealed class DetailsModel : PageModel
{
    private readonly JobDashboardService _jobDashboardService;

    public DetailsModel(JobDashboardService jobDashboardService)
    {
        _jobDashboardService = jobDashboardService;
    }

    public JobViewModel? Job { get; private set; }
    public int AutoRefreshSeconds { get; } = 60;

    public IActionResult OnGet(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToPage("/Index");
        }

        Job = _jobDashboardService.GetJob(id.Trim());
        if (Job is null)
        {
            return NotFound();
        }

        return Page();
    }
}
