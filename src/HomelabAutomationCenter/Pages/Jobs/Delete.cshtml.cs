using HomelabAutomationCenter.Models;
using HomelabAutomationCenter.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomelabAutomationCenter.Pages.Jobs;

public sealed class DeleteModel : PageModel
{
    private readonly JobConfigService _jobConfigService;
    private readonly JobDashboardService _jobDashboardService;
    private readonly JobConfigWriterService _jobConfigWriterService;

    public DeleteModel(
        JobConfigService jobConfigService,
        JobDashboardService jobDashboardService,
        JobConfigWriterService jobConfigWriterService)
    {
        _jobConfigService = jobConfigService;
        _jobDashboardService = jobDashboardService;
        _jobConfigWriterService = jobConfigWriterService;
    }

    [BindProperty]
    public bool DeleteStatusFile { get; set; }

    [BindProperty]
    public bool ForceDelete { get; set; }

    public JobViewModel? Job { get; private set; }
    public IReadOnlyList<JobConfig> DependentJobs { get; private set; } = [];
    public bool JobExists => Job is not null;
    public bool StatusFileExists => Job?.FileFound == true;

    public void OnGet(string id)
    {
        LoadJob(id);
    }

    public IActionResult OnPost(string id)
    {
        LoadJob(id);
        if (Job is null)
        {
            return Page();
        }

        if (DependentJobs.Count > 0 && !ForceDelete)
        {
            ModelState.AddModelError(string.Empty, "Deletion is blocked because other jobs depend on this job. Check force delete to override this safety check.");
            return Page();
        }

        var result = _jobConfigWriterService.DeleteJob(Job.Id, DeleteStatusFile, ForceDelete);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not delete the job.");
            return Page();
        }

        return RedirectToPage("/Index");
    }

    private void LoadJob(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            Job = null;
            DependentJobs = [];
            return;
        }

        var trimmedId = id.Trim();
        Job = _jobDashboardService.GetJob(trimmedId);
        var jobs = _jobConfigService.ReadJobs();
        DependentJobs = jobs
            .Where(job => job.DependsOn.Any(dependency => string.Equals(dependency, trimmedId, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(job => job.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
