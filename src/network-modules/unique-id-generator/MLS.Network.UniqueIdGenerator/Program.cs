using MLS.Network.UniqueIdGenerator.Controllers;
using MLS.Network.UniqueIdGenerator.Hubs;
using MLS.Network.UniqueIdGenerator.Models;
using MLS.Network.UniqueIdGenerator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<UniqueIdGeneratorConfig>(
    builder.Configuration.GetSection("UniqueIdGenerator"));

var cfg = builder.Configuration.GetSection("UniqueIdGenerator").Get<UniqueIdGeneratorConfig>()
          ?? new UniqueIdGeneratorConfig();

// ── Optional Redis ────────────────────────────────────────────────────────────
try
{
    var redis = StackExchange.Redis.ConnectionMultiplexer.Connect(cfg.RedisConnectionString);
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(redis);
}
catch (Exception ex)
{
    Console.WriteLine($"[UniqueIdGenerator] Redis unavailable: {ex.Message}");
}

// ── HTTP client for Block Controller ─────────────────────────────────────────
builder.Services.AddHttpClient<BlockControllerClient>(client =>
{
    client.BaseAddress = new Uri(cfg.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

// ── Core services ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IUniqueIdService, UniqueIdService>();

// ── Hosted services ───────────────────────────────────────────────────────────
builder.Services.AddHostedService(sp => sp.GetRequiredService<BlockControllerClient>());

// ── ASP.NET Core ──────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<UniqueIdGeneratorHub>("/hubs/unique-id-generator");
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    module = UniqueIdGeneratorConstants.ModuleName,
}));

app.Urls.Add($"http://0.0.0.0:{UniqueIdGeneratorConstants.HttpPort}");
app.Urls.Add($"http://0.0.0.0:{UniqueIdGeneratorConstants.WsPort}");

await app.RunAsync();
