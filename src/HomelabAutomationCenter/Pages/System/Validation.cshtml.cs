using HomelabAutomationCenter.Models;
using HomelabAutomationCenter.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomelabAutomationCenter.Pages.System;

public sealed class ValidationModel : PageModel
{
    private readonly SystemValidationService _systemValidationService;

    public ValidationModel(SystemValidationService systemValidationService)
    {
        _systemValidationService = systemValidationService;
    }

    public IReadOnlyList<ValidationResult> Results { get; private set; } = [];
    public int TotalChecks => Results.Count;
    public int ErrorCount => Results.Count(result => result.Status == ValidationStatus.Error);
    public int WarningCount => Results.Count(result => result.Status == ValidationStatus.Warning);
    public int PassedCount => Results.Count(result => result.Status == ValidationStatus.Pass);

    public void OnGet()
    {
        Results = _systemValidationService.Validate();
    }
}
