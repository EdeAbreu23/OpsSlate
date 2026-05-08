using OpsSlate.Models;
using OpsSlate.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OpsSlate.Pages.System;

public sealed class InfoModel : PageModel
{
    private readonly SystemInfoService _systemInfoService;

    public InfoModel(SystemInfoService systemInfoService)
    {
        _systemInfoService = systemInfoService;
    }

    public SystemInfo Info { get; private set; } = new();

    public void OnGet()
    {
        Info = _systemInfoService.GetInfo();
    }
}
