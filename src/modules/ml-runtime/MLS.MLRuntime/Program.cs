using MLS.MLRuntime.Configuration;
using MLS.MLRuntime.Hubs;
using MLS.MLRuntime.Inference;
using MLS.MLRuntime.Models;
using MLS.MLRuntime.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<MLRuntimeOptions>(builder.Configuration.GetSection("MLRuntime"));

var opts = builder.Configuration.GetSection("MLRuntime").Get<MLRuntimeOptions>()
           ?? new MLRuntimeOptions();

// ── Redis (optional — caching is disabled gracefully when unavailable) ─────────
try
{
    var redis = ConnectionMultiplexer.Connect(opts.RedisConnectionString);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
}
catch (Exception ex)
{
    Console.WriteLine($"[MLRuntime] Redis connection failed: {ex.Message} — caching disabled.");
}

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

// ── Core services ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IInferenceSessionFactory, DefaultInferenceSessionFactory>();
builder.Services.AddSingleton<IModelRegistry, ModelRegistry>();
builder.Services.AddSingleton<IInferenceEngine>(sp =>
{
    var registry  = sp.GetRequiredService<IModelRegistry>();
    var redisConn = sp.GetService<IConnectionMultiplexer>();   // nullable — Redis is optional
    var options   = sp.GetRequiredService<IOptions<MLRuntimeOptions>>();
    var logger    = sp.GetRequiredService<ILogger<InferenceEngine>>();
    return new InferenceEngine(registry, redisConn, options, logger);
});

// ── Hosted services ───────────────────────────────────────────────────────────
builder.Services.AddHostedService(sp => sp.GetRequiredService<BlockControllerClient>());
builder.Services.AddHostedService<InferenceWorker>();

// ── ASP.NET Core ──────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.WriteIndented = false);

builder.Services.AddSignalR(hub =>
{
    hub.EnableDetailedErrors = builder.Environment.IsDevelopment();
    hub.MaximumReceiveMessageSize = 512 * 1024;
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
app.MapHub<MLRuntimeHub>("/hubs/ml-runtime");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", module = "ml-runtime" }));

// ── Load startup models ───────────────────────────────────────────────────────
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
{
    var registry = app.Services.GetRequiredService<IModelRegistry>();
    var mlOpts   = app.Services.GetRequiredService<IOptions<MLRuntimeOptions>>().Value;
    var logger   = app.Services.GetRequiredService<ILogger<Program>>();

    var startupModels = new[]
    {
        ("model-t", mlOpts.ModelTPath),
        ("model-a", mlOpts.ModelAPath),
        ("model-d", mlOpts.ModelDPath),
    };

    foreach (var (key, path) in startupModels)
    {
        if (string.IsNullOrWhiteSpace(path)) continue;

        _ = Task.Run(async () =>
        {
            try
            {
                await registry.LoadAsync(key, path).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load startup model key={Key} path={Path}", key, path);
            }
        });
    }
});

// HTTP port 5600 + WebSocket/SignalR port 6600
app.Urls.Add("http://0.0.0.0:5600");
app.Urls.Add("http://0.0.0.0:6600");

await app.RunAsync();
