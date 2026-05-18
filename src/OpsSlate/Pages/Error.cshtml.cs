using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OpsSlate.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ErrorModel : PageModel
{
    public string? RequestId { get; private set; }

    public void OnGet()
    {
        SetRequestId();
    }

    public void OnPost()
    {
        SetRequestId();
    }

    private void SetRequestId()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}
