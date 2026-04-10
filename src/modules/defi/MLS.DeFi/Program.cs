using Microsoft.EntityFrameworkCore;
using MLS.DeFi.Addresses;
using MLS.DeFi.Configuration;
using MLS.DeFi.Hubs;
using MLS.DeFi.Interfaces;
using MLS.DeFi.Persistence;
using MLS.DeFi.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<DeFiOptions>(builder.Configuration.GetSection("DeFi"));

var opts = builder.Configuration.GetSection("DeFi").Get<DeFiOptions>() ?? new DeFiOptions();

// ── HTTP clients ──────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<BlockControllerClient>(client =>
{
    client.BaseAddress = new Uri(opts.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<IHyperliquidClient, HyperliquidClient>(client =>
{
    client.BaseAddress = new Uri(opts.HyperliquidRestUrl);
    client.Timeout     = TimeSpan.FromSeconds(15);
});

// Named client for on-chain JSON-RPC calls
builder.Services.AddHttpClient("chain-rpc", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ── PostgreSQL / EF Core ──────────────────────────────────────────────────────
builder.Services.AddDbContextFactory<DeFiDbContext>(o =>
    o.UseNpgsql(opts.PostgresConnectionString));

builder.Services.AddDbContext<DeFiDbContext>(o =>
    o.UseNpgsql(opts.PostgresConnectionString));

builder.Services.AddScoped<TransactionRepository>();

// ── Redis ─────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(opts.RedisConnectionString));

// ── DeFi services ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IDeFiAddressBook, AddressBook>();
builder.Services.AddSingleton<IWalletProvider,  WalletProvider>();
builder.Services.AddSingleton<IOnChainTransactionService, OnChainTransactionService>();
builder.Services.AddSingleton<IBrokerFallbackChain, BrokerFallbackChain>();
builder.Services.AddSingleton<IDeFiStrategyEngine,  DeFiStrategyEngine>();

// ── Hosted services ───────────────────────────────────────────────────────────
builder.Services.AddHostedService(sp => sp.GetRequiredService<BlockControllerClient>());
builder.Services.AddHostedService<PositionMonitorService>();

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
app.MapHub<DeFiHub>("/hubs/defi");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", module = "defi" }));

// Ensure database schema on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DeFiDbContext>();
    await db.Database.MigrateAsync();
}

// HTTP port 5500 + WebSocket/SignalR port 6500 — both served by the same Kestrel instance.
app.Urls.Add("http://0.0.0.0:5500");
app.Urls.Add("http://0.0.0.0:6500");

await app.RunAsync();
