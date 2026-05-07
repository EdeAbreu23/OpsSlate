using HomelabAutomationCenter.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSingleton<JobConfigService>();
builder.Services.AddSingleton<JobStatusService>();
builder.Services.AddSingleton<JobHealthEvaluator>();
builder.Services.AddSingleton<JobDashboardService>();
builder.Services.AddSingleton<SystemValidationService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
