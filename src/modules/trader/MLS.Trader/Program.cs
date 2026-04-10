using Microsoft.EntityFrameworkCore;
using MLS.Trader.Configuration;
using MLS.Trader.Hubs;
using MLS.Trader.Interfaces;
using MLS.Trader.Orders;
using MLS.Trader.Persistence;
using MLS.Trader.Risk;
using MLS.Trader.Services;
using MLS.Trader.Signals;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<TraderOptions>(builder.Configuration.GetSection("Trader"));

var opts = builder.Configuration.GetSection("Trader").Get<TraderOptions>()
           ?? new TraderOptions();

// ── HTTP clients ──────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<BlockControllerClient>(client =>
{
    client.BaseAddress = new Uri(opts.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<IEnvelopeSender, EnvelopeSender>(client =>
{
    client.BaseAddress = new Uri(opts.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

// ── PostgreSQL / EF Core ──────────────────────────────────────────────────────
builder.Services.AddDbContextFactory<TraderDbContext>(o =>
    o.UseNpgsql(opts.PostgresConnectionString));

builder.Services.AddDbContext<TraderDbContext>(o =>
    o.UseNpgsql(opts.PostgresConnectionString));

// ── Redis (optional) ──────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    try
    {
        return ConnectionMultiplexer.Connect(opts.RedisConnectionString);
    }
    catch (Exception ex)
    {
        var log = sp.GetRequiredService<ILogger<Program>>();
        log.LogWarning(ex, "Redis unavailable — position cache will use in-memory only");
        return ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false");
    }
});

// ── Module identity (shared between BlockControllerClient and MarketDataWorker) ──
builder.Services.AddSingleton<ModuleIdentity>();

// ── Trader services ───────────────────────────────────────────────────────────
builder.Services.AddSingleton<ISignalEngine, SignalEngine>();
builder.Services.AddSingleton<IRiskManager, RiskManager>();
builder.Services.AddSingleton<IOrderManager, OrderManager>();
builder.Services.AddSingleton<MarketDataWorker>();

// ── Hosted services ───────────────────────────────────────────────────────────
builder.Services.AddHostedService(sp => sp.GetRequiredService<BlockControllerClient>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MarketDataWorker>());

// ── ASP.NET Core ──────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.WriteIndented = false);

builder.Services.AddSignalR(hub =>
{
    hub.EnableDetailedErrors = builder.Environment.IsDevelopment();
    hub.MaximumReceiveMessageSize = 1024 * 1024;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Build & configure pipeline ────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<TraderHub>("/hubs/trader");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", module = "trader" }));

// Ensure database schema on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TraderDbContext>();
    await db.Database.MigrateAsync();
}

// HTTP port 5300 + WebSocket/SignalR port 6300 — both served by the same Kestrel instance.
app.Urls.Add("http://0.0.0.0:5300");
app.Urls.Add("http://0.0.0.0:6300");

await app.RunAsync();
