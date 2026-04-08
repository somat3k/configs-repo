using MLS.WorkflowDemo.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("default", c =>
{
    c.Timeout = TimeSpan.FromSeconds(12);
    c.DefaultRequestHeaders.Add("User-Agent", "MLS-WorkflowDemo/1.0");
});

builder.Services.AddScoped<MLS.WorkflowDemo.Services.WorkflowDataService>();

builder.Services.AddHealthChecks();

builder.Logging.AddConsole();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHealthChecks("/health");

// Default to localhost; override with ASPNETCORE_URLS for LAN / container access.
app.Urls.Add("http://localhost:5099");

app.Logger.LogInformation("MLS WorkflowDemo starting on http://localhost:5099");
app.Logger.LogInformation("Workflow index: http://localhost:5099/workflow");

await app.RunAsync().ConfigureAwait(false);
