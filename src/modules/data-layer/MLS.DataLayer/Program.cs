using Microsoft.EntityFrameworkCore;
using MLS.DataLayer.Configuration;
using MLS.DataLayer.Hubs;
using MLS.DataLayer.Hydra;
using MLS.DataLayer.Persistence;
using MLS.DataLayer.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<DataLayerOptions>(builder.Configuration.GetSection("DataLayer"));

var opts = builder.Configuration.GetSection("DataLayer").Get<DataLayerOptions>()
           ?? new DataLayerOptions();

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

// BackfillPipeline uses a typed HttpClient for HYPERLIQUID / Camelot REST calls
builder.Services.AddHttpClient<BackfillPipeline>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// CamelotFeedCollector uses a typed HttpClient for subgraph polling
builder.Services.AddHttpClient<CamelotFeedCollector>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

// ── PostgreSQL / EF Core ──────────────────────────────────────────────────────
var pgConnStr = opts.PostgresConnectionString;

// Pooled DbContextFactory for GapDetector (creates scoped contexts on demand)
builder.Services.AddDbContextFactory<DataLayerDbContext>(o =>
    o.UseNpgsql(pgConnStr));

// Scoped DbContext for CandleRepository (used inside DI scopes created by FeedScheduler)
builder.Services.AddDbContext<DataLayerDbContext>(o =>
    o.UseNpgsql(pgConnStr));

builder.Services.AddScoped<CandleRepository>();

// ── Hydra collectors (singleton — share HttpClient factory cache) ─────────────
builder.Services.AddSingleton<HyperliquidFeedCollector>();
builder.Services.AddSingleton<CamelotFeedCollector>();

// ── Hydra pipeline services ───────────────────────────────────────────────────
builder.Services.AddSingleton<FeedScheduler>();
builder.Services.AddSingleton<BackfillPipeline>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<BackfillPipeline>());
builder.Services.AddHostedService<GapDetector>();

// ── Block Controller registration + heartbeat ─────────────────────────────────
builder.Services.AddHostedService<BlockControllerClient>();

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
app.MapHub<DataLayerHub>("/hubs/data-layer");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", module = "data-layer" }));

// Ensure database schema on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataLayerDbContext>();
    await db.Database.MigrateAsync();
}

await app.RunAsync();
