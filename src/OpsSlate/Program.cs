using OpsSlate.Options;
using OpsSlate.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.Configure<OpsSlatePathOptions>(options =>
{
    var configPath = builder.Configuration["HAC_CONFIG_PATH"];
    if (!string.IsNullOrWhiteSpace(configPath))
    {
        options.ConfigPath = configPath;
    }

    var statusRoot = builder.Configuration["HAC_STATUS_ROOT"];
    if (!string.IsNullOrWhiteSpace(statusRoot))
    {
        options.StatusRoot = statusRoot;
    }
});
builder.Services.AddSingleton<JobConfigService>();
builder.Services.AddSingleton<JobConfigWriterService>();
builder.Services.AddSingleton<JobConfigEditService>();
builder.Services.AddSingleton<JobStatusService>();
builder.Services.AddSingleton<TimeFormatter>();
builder.Services.AddSingleton<JobHealthEvaluator>();
builder.Services.AddSingleton<JobDashboardService>();
builder.Services.AddSingleton<SystemValidationService>();
builder.Services.AddSingleton<SystemInfoService>();

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
