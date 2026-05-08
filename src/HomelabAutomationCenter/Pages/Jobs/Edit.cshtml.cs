using System.ComponentModel.DataAnnotations;
using HomelabAutomationCenter.Models;
using HomelabAutomationCenter.Options;
using HomelabAutomationCenter.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace HomelabAutomationCenter.Pages.Jobs;

public sealed class EditModel : PageModel
{
    private readonly JobConfigEditService _jobConfigEditService;
    private readonly JobConfigService _jobConfigService;
    private readonly HacPathOptions _pathOptions;

    public EditModel(
        JobConfigEditService jobConfigEditService,
        JobConfigService jobConfigService,
        IOptions<HacPathOptions> pathOptions)
    {
        _jobConfigEditService = jobConfigEditService;
        _jobConfigService = jobConfigService;
        _pathOptions = pathOptions.Value;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<JobConfig> ExistingJobs { get; private set; } = [];
    public string ConfigPath => _pathOptions.ConfigPath;
    public string StatusRoot => _pathOptions.StatusRoot;
    public string? StatusWarning { get; private set; }

    public IActionResult OnGet(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToPage("/Index");
        }

        ExistingJobs = _jobConfigService.ReadJobs();
        var job = _jobConfigEditService.GetJob(id.Trim());
        if (job is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            JobId = job.Id,
            JobName = job.Name,
            StatusPath = job.StatusPath,
            StaleAfterMinutes = job.StaleAfterMinutes,
            DependsOn = string.Join(", ", job.DependsOn)
        };

        return Page();
    }

    public IActionResult OnPost(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToPage("/Index");
        }

        ExistingJobs = _jobConfigService.ReadJobs();
        Input.JobId = id.Trim();
        NormalizeInput();
        ModelState.Clear();
        TryValidateModel(Input, nameof(Input));
        ValidateInput();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = _jobConfigEditService.UpdateJob(new EditableJob(
            Input.JobId,
            Input.JobName,
            Input.StatusPath,
            Input.StaleAfterMinutes,
            ParseDependencies(Input.DependsOn).ToList()));

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not update the job config.");
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(result.StatusWarning))
        {
            StatusWarning = result.StatusWarning;
        }

        return RedirectToPage("/Jobs/Details", new { id = Input.JobId });
    }

    private void NormalizeInput()
    {
        Input.JobId = Input.JobId.Trim();
        Input.JobName = Input.JobName.Trim();
        Input.StatusPath = Input.StatusPath.Trim();
        Input.DependsOn = Input.DependsOn?.Trim() ?? string.Empty;
    }

    private void ValidateInput()
    {
        var currentJobExists = ExistingJobs.Any(job => string.Equals(job.Id, Input.JobId, StringComparison.OrdinalIgnoreCase));
        if (!currentJobExists)
        {
            ModelState.AddModelError(string.Empty, $"Job '{Input.JobId}' does not exist.");
        }

        var dependencies = ParseDependencies(Input.DependsOn).ToList();
        var duplicateDependencies = dependencies
            .GroupBy(dependency => dependency, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateDependencies.Count > 0)
        {
            ModelState.AddModelError("Input.DependsOn", $"Duplicate dependency ID(s) are not allowed: {string.Join(", ", duplicateDependencies)}.");
        }

        var existingIds = ExistingJobs.Select(job => job.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingDependencies = dependencies
            .Where(dependency => !existingIds.Contains(dependency))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missingDependencies.Count > 0)
        {
            ModelState.AddModelError("Input.DependsOn", $"Dependencies must reference existing job IDs: {string.Join(", ", missingDependencies)}.");
        }

        if (dependencies.Any(dependency => string.Equals(dependency, Input.JobId, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("Input.DependsOn", "A job cannot depend on itself.");
        }
    }

    private static IEnumerable<string> ParseDependencies(string? dependsOn)
    {
        if (string.IsNullOrWhiteSpace(dependsOn))
        {
            return [];
        }

        return dependsOn
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency));
    }

    public sealed class InputModel
    {
        [Display(Name = "Job ID")]
        public string JobId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Job Name is required.")]
        [Display(Name = "Job Name")]
        public string JobName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Status path is required.")]
        [Display(Name = "Status path")]
        public string StatusPath { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Stale after minutes must be greater than 0.")]
        [Display(Name = "Stale after minutes")]
        public int StaleAfterMinutes { get; set; } = 60;

        [Display(Name = "Depends on job IDs")]
        public string DependsOn { get; set; } = string.Empty;
    }
}
