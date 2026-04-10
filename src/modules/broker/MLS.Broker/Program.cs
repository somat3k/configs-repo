using Microsoft.EntityFrameworkCore;
using MLS.Broker.Configuration;
using MLS.Broker.Hubs;
using MLS.Broker.Interfaces;
using MLS.Broker.Persistence;
using MLS.Broker.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<BrokerOptions>(builder.Configuration.GetSection("Broker"));

var opts = builder.Configuration.GetSection("Broker").Get<BrokerOptions>()
           ?? new BrokerOptions();

// ── HTTP clients ──────────────────────────────────────────────────────────────
// BlockControllerClient is registered as a typed HTTP client; the hosted-service
// registration resolves the same instance so it gets the configured HttpClient.
builder.Services.AddHttpClient<BlockControllerClient>(client =>
{
    client.BaseAddress = new Uri(opts.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

// HyperliquidClient: registered as typed client against IHyperliquidClient so the
// DI-provided HttpClient (with BaseAddress/Timeout) is always used — no conflicting
// singleton registration.
builder.Services.AddHttpClient<IHyperliquidClient, HyperliquidClient>(client =>
{
    client.BaseAddress = new Uri(opts.HyperliquidRestUrl);
    client.Timeout     = TimeSpan.FromSeconds(15);
});

// ── PostgreSQL / EF Core ──────────────────────────────────────────────────────
builder.Services.AddDbContextFactory<BrokerDbContext>(o =>
    o.UseNpgsql(opts.PostgresConnectionString));

builder.Services.AddDbContext<BrokerDbContext>(o =>
    o.UseNpgsql(opts.PostgresConnectionString));

builder.Services.AddScoped<OrderRepository>();

// ── Redis ─────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(opts.RedisConnectionString));

// ── Broker services ───────────────────────────────────────────────────────────
builder.Services.AddSingleton<IBrokerFallbackChain, BrokerFallbackChain>();
builder.Services.AddSingleton<IOrderTracker, OrderTracker>();

// ── Hosted services ───────────────────────────────────────────────────────────
// Resolve BlockControllerClient from the typed-client registration so it gets
// the configured HttpClient (BaseAddress + Timeout set above).
builder.Services.AddHostedService(sp => sp.GetRequiredService<BlockControllerClient>());
builder.Services.AddHostedService<FillNotificationService>();

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
app.MapHub<BrokerHub>("/hubs/broker");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", module = "broker" }));

// Ensure database schema on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BrokerDbContext>();
    await db.Database.MigrateAsync();
}

// HTTP port 5800 + WebSocket/SignalR port 6800 — both served by the same Kestrel instance.
app.Urls.Add("http://0.0.0.0:5800");
app.Urls.Add("http://0.0.0.0:6800");

await app.RunAsync();
