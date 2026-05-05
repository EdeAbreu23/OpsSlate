using HomelabAutomationCenter.Models;
using HomelabAutomationCenter.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomelabAutomationCenter.Pages;

public sealed class IndexModel : PageModel
{
    private readonly JobConfigService _jobConfigService;
    private readonly JobStatusService _jobStatusService;
    private readonly JobHealthEvaluator _jobHealthEvaluator;

    public IndexModel(
        JobConfigService jobConfigService,
        JobStatusService jobStatusService,
        JobHealthEvaluator jobHealthEvaluator)
    {
        _jobConfigService = jobConfigService;
        _jobStatusService = jobStatusService;
        _jobHealthEvaluator = jobHealthEvaluator;
    }

    public IReadOnlyList<JobViewModel> Jobs { get; private set; } = [];

    public void OnGet()
    {
        var jobs = _jobConfigService.ReadJobs();
        Jobs = jobs
            .Select(job =>
            {
                var read = _jobStatusService.ReadStatus(job.StatusPath);
                return _jobHealthEvaluator.Evaluate(job, read.fileFound, read.isValidJson, read.status);
            })
            .OrderBy(j => j.Name)
            .ToList();
    }
}
