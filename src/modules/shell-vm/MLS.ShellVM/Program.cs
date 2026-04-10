using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MLS.ShellVM.Background;
using MLS.ShellVM.Controllers;
using MLS.ShellVM.Hubs;
using MLS.ShellVM.Interfaces;
using MLS.ShellVM.Models;
using MLS.ShellVM.Persistence;
using MLS.ShellVM.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<ShellVMConfig>(builder.Configuration.GetSection("ShellVM"));

var cfg = builder.Configuration.GetSection("ShellVM").Get<ShellVMConfig>()
          ?? new ShellVMConfig();

// ── PostgreSQL / EF Core ──────────────────────────────────────────────────────
builder.Services.AddDbContextFactory<ShellVMDbContext>(o =>
    o.UseNpgsql(cfg.PostgresConnectionString));

builder.Services.AddDbContext<ShellVMDbContext>(o =>
    o.UseNpgsql(cfg.PostgresConnectionString));

// ── Redis (optional — session persistence degrades gracefully when unavailable) ─
try
{
    var redis = ConnectionMultiplexer.Connect(cfg.RedisConnectionString);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
}
catch (Exception ex)
{
    Console.WriteLine($"[ShellVM] Redis connection failed: {ex.Message} — session persistence disabled.");
    // IConnectionMultiplexer is NOT registered; sp.GetService<IConnectionMultiplexer>() returns null.
}

// ── HTTP clients ──────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<BlockControllerClient>(client =>
{
    client.BaseAddress = new Uri(cfg.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

// ── Core services ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IPtyProvider, PtyProviderService>();

builder.Services.AddSingleton<ISessionManager>(sp => new SessionManager(
    sp.GetRequiredService<IDbContextFactory<ShellVMDbContext>>(),
    sp.GetService<IConnectionMultiplexer>(),                // nullable — Redis is optional
    sp.GetRequiredService<IPtyProvider>(),
    sp.GetRequiredService<IOptions<ShellVMConfig>>(),
    sp.GetRequiredService<ILogger<SessionManager>>()));

builder.Services.AddSingleton<IAuditLogger, AuditLogger>();

// OutputBroadcaster depends on IHubContext which requires the hub to be registered first
// — it is registered after AddSignalR below.

builder.Services.AddSingleton<IExecutionEngine, ExecutionEngine>();

// ── Hosted services ───────────────────────────────────────────────────────────
builder.Services.AddHostedService(sp => sp.GetRequiredService<BlockControllerClient>());
builder.Services.AddHostedService<HeartbeatService>();
builder.Services.AddHostedService<SessionWatchdog>();

// ── ASP.NET Core ──────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddControllersAsServices()        // enables factory-based registration for controllers
    .AddJsonOptions(o => o.JsonSerializerOptions.WriteIndented = false);

// Register SessionsController with factory so IConnectionMultiplexer? resolves optionally
builder.Services.AddTransient<SessionsController>(sp => new SessionsController(
    sp.GetRequiredService<ISessionManager>(),
    sp.GetRequiredService<IExecutionEngine>(),
    sp.GetRequiredService<IAuditLogger>(),
    sp.GetRequiredService<IPtyProvider>(),
    sp.GetRequiredService<IHubContext<ShellVMHub>>(),
    sp.GetService<IConnectionMultiplexer>(),
    sp.GetRequiredService<IOptions<ShellVMConfig>>(),
    sp.GetRequiredService<ILogger<SessionsController>>()));

builder.Services.AddSignalR(hub =>
{
    hub.EnableDetailedErrors = builder.Environment.IsDevelopment();
    hub.MaximumReceiveMessageSize = 2 * 1024 * 1024;   // 2 MB
});

// OutputBroadcaster registered after SignalR so IHubContext<ShellVMHub> is resolvable
builder.Services.AddSingleton<IOutputBroadcaster>(sp => new OutputBroadcaster(
    sp.GetRequiredService<IHubContext<ShellVMHub>>(),
    sp.GetService<IConnectionMultiplexer>(),                // nullable — Redis is optional
    sp.GetRequiredService<IOptions<ShellVMConfig>>(),
    sp.GetRequiredService<ILogger<OutputBroadcaster>>()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Build & configure pipeline ─────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<ShellVMHub>("/hubs/shell-vm");

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    module = ShellVMNetworkConstants.ModuleName,
}));

// Ensure database schema is current
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShellVMDbContext>();
    await db.Database.EnsureCreatedAsync();

    // Create partitioned audit log table if it does not exist (EF Core does not model partitions)
    await db.Database.ExecuteSqlRawAsync("""
        CREATE EXTENSION IF NOT EXISTS pgcrypto;

        CREATE TABLE IF NOT EXISTS shell_audit_log (
            id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            block_id    UUID,
            command     TEXT NOT NULL,
            started_at  TIMESTAMPTZ NOT NULL,
            ended_at    TIMESTAMPTZ,
            exit_code   INT,
            duration_ms BIGINT,
            module_id   TEXT
        );
        """);
}

// HTTP port 5950 + WebSocket/SignalR port 6950 — both served by the same Kestrel instance
app.Urls.Add($"http://0.0.0.0:{ShellVMNetworkConstants.HttpPort}");
app.Urls.Add($"http://0.0.0.0:{ShellVMNetworkConstants.WsPort}");

await app.RunAsync();
