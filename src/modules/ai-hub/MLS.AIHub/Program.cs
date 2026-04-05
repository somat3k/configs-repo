using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using MLS.AIHub.Configuration;
using MLS.AIHub.Persistence;
using MLS.AIHub.Providers;
using MLS.AIHub.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<AIHubOptions>(builder.Configuration.GetSection("AIHub"));

var aiHubOpts = builder.Configuration.GetSection("AIHub").Get<AIHubOptions>()
                ?? new AIHubOptions();

// ── HTTP clients ──────────────────────────────────────────────────────────────
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
// All six providers registered as ILLMProvider implementations.
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
builder.Services.AddHostedService<BlockControllerClient>();

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

// ── Ensure DB schema exists ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AIHubDbContext>();
    await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
}

app.Logger.LogInformation(
    "AI Hub starting on HTTP {Http} / WS {Ws}",
    aiHubOpts.HttpEndpoint,
    aiHubOpts.WsEndpoint);

await app.RunAsync().ConfigureAwait(false);
