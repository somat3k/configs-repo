using Microsoft.FluentUI.AspNetCore.Components;
using MLS.WebApp.Components.Canvas;
using MLS.WebApp.Hubs;
using MLS.WebApp.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
var cfg = builder.Configuration;

// ── Blazor + FluentUI ─────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFluentUIComponents();

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR(hub =>
{
    hub.EnableDetailedErrors = builder.Environment.IsDevelopment();
    hub.MaximumReceiveMessageSize = 1024 * 512;
});

// ── Block Controller hub client (fan-out relay) ───────────────────────────────
builder.Services.AddSingleton<IBlockControllerHub, BlockControllerHub>();

// ── MDI Window Manager (scoped — one per browser session) ────────────────────
builder.Services.AddScoped<IWindowLayoutService, WindowLayoutService>();
builder.Services.AddScoped<WindowManager>();

// ── Block Controller registration + heartbeat (background) ───────────────────
builder.Services.AddHttpClient<BlockControllerClient>(client =>
{
    client.BaseAddress = new Uri(
        cfg["MLS:Network:BlockControllerUrl"] ?? "http://block-controller:5100");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHostedService<BlockControllerClient>();

// ── REST + Swagger ────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "MLS Web App", Version = "v1" }));

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

builder.Logging.AddConsole();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<MLS.WebApp.App>()
    .AddInteractiveServerRenderMode();

app.MapHub<DashboardHub>("/hubs/dashboard");
app.MapControllers();
app.MapHealthChecks("/health");

// HTTP API port 5200 + WebSocket port 6200
app.Urls.Add("http://0.0.0.0:5200");
app.Urls.Add("http://0.0.0.0:6200");

app.Logger.LogInformation("MLS Web App starting on HTTP 5200 / WS 6200");

await app.RunAsync().ConfigureAwait(false);

#pragma warning disable CS1591
public partial class Program { }
#pragma warning restore CS1591
