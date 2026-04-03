using MLS.BlockController.Hubs;
using MLS.BlockController.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ─────────────────────────────────────────────────────────────────
// Note: all payload records carry explicit [JsonPropertyName] attributes for snake_case
// wire names. The PropertyNamingPolicy is intentionally NOT set here; the attributes
// are the authoritative, self-contained wire contract.
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.WriteIndented = false;
    });

builder.Services.AddSignalR(hub =>
{
    hub.EnableDetailedErrors = builder.Environment.IsDevelopment();
    hub.MaximumReceiveMessageSize = 1024 * 1024; // 1 MB
});

builder.Services.AddSingleton<ISubscriptionTable, SubscriptionTable>();
builder.Services.AddSingleton<IModuleRegistry, InMemoryModuleRegistry>();
builder.Services.AddSingleton<IMessageRouter, InMemoryMessageRouter>();
builder.Services.AddSingleton<IStrategyRouter, StrategyRouter>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MLS Block Controller", Version = "v1" });
});

builder.Logging.AddConsole();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<BlockControllerHub>("/hubs/block-controller");

// Both URLs bind to the same Kestrel instance; all endpoints (HTTP REST + SignalR hub)
// are reachable on both ports. Port 5100 is the primary HTTP API port;
// port 6100 is the designated WebSocket/SignalR port by platform convention.
app.Urls.Add("http://0.0.0.0:5100");
app.Urls.Add("http://0.0.0.0:6100");

app.Run();

// Expose the generated Program class for WebApplicationFactory in integration tests.
#pragma warning disable CS1591
public partial class Program { }
#pragma warning restore CS1591
