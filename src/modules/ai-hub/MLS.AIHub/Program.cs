using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using MLS.AIHub.Canvas;
using MLS.AIHub.Configuration;
using MLS.AIHub.Context;
using MLS.AIHub.Hubs;
using MLS.AIHub.Persistence;
using MLS.AIHub.Plugins;
using MLS.AIHub.Providers;
using MLS.AIHub.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<AIHubOptions>(builder.Configuration.GetSection("AIHub"));

var aiHubOpts = builder.Configuration.GetSection("AIHub").Get<AIHubOptions>()
                ?? new AIHubOptions();

// ── HTTP clients ──────────────────────────────────────────────────────────────
// BlockControllerClient is registered as a typed HTTP client so the HttpClient
// it receives has the correct BaseAddress and Timeout pre-configured.
builder.Services.AddHttpClient<BlockControllerClient>(client =>
{
    client.BaseAddress = new Uri(aiHubOpts.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

// Register a named HttpClient for providers that probe remote APIs
builder.Services.AddHttpClient();

// ── PostgreSQL / EF Core ──────────────────────────────────────────────────────
builder.Services.AddDbContext<AIHubDbContext>(options =>
    options.UseNpgsql(aiHubOpts.PostgresConnectionString));

builder.Services.AddScoped<IUserPreferenceRepository, UserPreferenceRepository>();

// ── LLM Providers ─────────────────────────────────────────────────────────────
// All seven providers registered as ILLMProvider implementations.
// ProviderRouter receives IEnumerable<ILLMProvider> to walk the full set.
builder.Services.AddSingleton<ILLMProvider, OpenAIProvider>();
builder.Services.AddSingleton<ILLMProvider, AnthropicProvider>();
builder.Services.AddSingleton<ILLMProvider, GoogleProvider>();
builder.Services.AddSingleton<ILLMProvider, GroqProvider>();
builder.Services.AddSingleton<ILLMProvider, OpenRouterProvider>();
builder.Services.AddSingleton<ILLMProvider, VercelAIProvider>();
builder.Services.AddSingleton<ILLMProvider, LocalProvider>();

// ── Provider Router ────────────────────────────────────────────────────────────
builder.Services.AddScoped<IProviderRouter, ProviderRouter>();

// ── Context Assembler ─────────────────────────────────────────────────────────
builder.Services.AddScoped<IContextAssembler, ContextAssembler>();

// ── Canvas Action Dispatcher ──────────────────────────────────────────────────
builder.Services.AddScoped<ICanvasActionDispatcher, CanvasActionDispatcher>();

// ── Canvas Action Counter (scoped — tracks per-request canvas dispatches) ─────
builder.Services.AddScoped<ICanvasActionCounter, CanvasActionCounter>();

// ── Chat Service ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IChatService, ChatService>();

// ── Chat Request Queue (bounded Channel<T> — protects against load spikes) ───
builder.Services.AddSingleton<ChatQueueProcessor>();
builder.Services.AddSingleton<IChatRequestQueue>(sp => sp.GetRequiredService<ChatQueueProcessor>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ChatQueueProcessor>());

// ── Semantic Kernel Plugins ───────────────────────────────────────────────────
builder.Services.AddScoped<TradingPlugin>();
builder.Services.AddScoped<DesignerPlugin>();
builder.Services.AddScoped<AnalyticsPlugin>();
builder.Services.AddScoped<MLRuntimePlugin>();
builder.Services.AddScoped<DeFiPlugin>();

// ── Semantic Kernel ───────────────────────────────────────────────────────────
// Kernel is registered as a factory — each request gets a fresh kernel scoped
// to the selected provider and model.
builder.Services.AddTransient<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.Services.AddLogging(l => l.AddConsole());
    return kernelBuilder.Build();
});

// ── Block Controller registration + heartbeat ─────────────────────────────────
// Resolve via the typed-client registration so HttpClient has BaseAddress configured.
builder.Services.AddHostedService(sp => sp.GetRequiredService<BlockControllerClient>());

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR(hub =>
{
    hub.EnableDetailedErrors = builder.Environment.IsDevelopment();
    hub.MaximumReceiveMessageSize = 1024 * 1024;
});

// ── REST controllers + Swagger ────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts => opts.JsonSerializerOptions.WriteIndented = false);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MLS AI Hub", Version = "v1" });
});

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── Build & configure pipeline ────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHub<MLS.AIHub.Hubs.AIHub>("/hubs/ai-hub");

// ── Ensure DB schema exists ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AIHubDbContext>();
    await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
}

// HTTP API port 5750 + WebSocket port 6750 — same Kestrel instance, both ports active
app.Urls.Add("http://0.0.0.0:5750");
app.Urls.Add("http://0.0.0.0:6750");

app.Logger.LogInformation(
    "AI Hub starting on HTTP {Http} / WS {Ws}",
    aiHubOpts.HttpEndpoint,
    aiHubOpts.WsEndpoint);

await app.RunAsync().ConfigureAwait(false);
