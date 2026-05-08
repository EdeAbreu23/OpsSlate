using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using HomelabAutomationCenter.Models;
using HomelabAutomationCenter.Options;
using HomelabAutomationCenter.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace HomelabAutomationCenter.Pages.Jobs;

public sealed partial class NewModel : PageModel
{
    private readonly JobConfigService _jobConfigService;
    private readonly JobConfigWriterService _jobConfigWriterService;
    private readonly HacPathOptions _pathOptions;

    public NewModel(
        JobConfigService jobConfigService,
        JobConfigWriterService jobConfigWriterService,
        IOptions<HacPathOptions> pathOptions)
    {
        _jobConfigService = jobConfigService;
        _jobConfigWriterService = jobConfigWriterService;
        _pathOptions = pathOptions.Value;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<JobConfig> ExistingJobs { get; private set; } = [];
    public string ConfigPath => _pathOptions.ConfigPath;
    public string StatusRoot => _pathOptions.StatusRoot;
    public string? StatusWarning { get; private set; }

    public void OnGet()
    {
        ExistingJobs = _jobConfigService.ReadJobs();
        Input.StaleAfterMinutes = 60;
    }

    public IActionResult OnPost()
    {
        ExistingJobs = _jobConfigService.ReadJobs();
        NormalizeInput();
        ModelState.Clear();
        TryValidateModel(Input, nameof(Input));
        ValidateInput();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = _jobConfigWriterService.AddJob(new JobConfig
        {
            Id = Input.JobId,
            Name = Input.JobName,
            StatusPath = Input.StatusPath,
            StaleAfterMinutes = Input.StaleAfterMinutes,
            DependsOn = ParseDependencies(Input.DependsOn).ToList()
        });

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not save the job config.");
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(result.StatusWarning))
        {
            StatusWarning = result.StatusWarning;
        }

        return RedirectToPage("/Index");
    }

    public string BuildDefaultStatusPath(string? jobId = null)
    {
        var id = string.IsNullOrWhiteSpace(jobId) ? "<job_id>" : jobId.Trim();
        return CombineStatusPath(_pathOptions.StatusRoot, id, "status.json");
    }

    private void NormalizeInput()
    {
        Input.JobId = Input.JobId.Trim();
        Input.JobName = Input.JobName.Trim();
        Input.StatusPath = Input.StatusPath.Trim();
        Input.DependsOn = Input.DependsOn?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(Input.StatusPath) && !string.IsNullOrWhiteSpace(Input.JobId))
        {
            Input.StatusPath = BuildDefaultStatusPath(Input.JobId);
        }
    }

    private void ValidateInput()
    {
        if (!string.IsNullOrWhiteSpace(Input.JobId) && !JobIdPattern().IsMatch(Input.JobId))
        {
            ModelState.AddModelError("Input.JobId", "Job ID may only contain lowercase letters, numbers, underscores, or dashes.");
        }

        if (ExistingJobs.Any(job => string.Equals(job.Id, Input.JobId, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("Input.JobId", "Job ID must be unique. Another job already uses this ID.");
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

    private static string CombineStatusPath(string root, params string[] parts)
    {
        var trimmedRoot = string.IsNullOrWhiteSpace(root) ? HacPathOptions.DefaultStatusRoot : root.TrimEnd('/');
        var suffix = string.Join('/', parts.Select(part => part.Trim('/')).Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(suffix) ? trimmedRoot : $"{trimmedRoot}/{suffix}";
    }

    [GeneratedRegex("^[a-z0-9_-]+$")]
    private static partial Regex JobIdPattern();

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Job ID is required.")]
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
