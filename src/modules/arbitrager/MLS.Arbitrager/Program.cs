using MLS.Arbitrager.Addresses;
using MLS.Arbitrager.Configuration;
using MLS.Arbitrager.Execution;
using MLS.Arbitrager.Hubs;
using MLS.Arbitrager.Scanning;
using MLS.Arbitrager.Scoring;
using MLS.Arbitrager.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<ArbitragerOptions>(builder.Configuration.GetSection("Arbitrager"));

var opts = builder.Configuration.GetSection("Arbitrager").Get<ArbitragerOptions>()
           ?? new ArbitragerOptions();

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

builder.Services.AddHttpClient("price-feed", client =>
{
    client.Timeout = TimeSpan.FromSeconds(8);
});

// ── Core services ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IOpportunityScanner, OpportunityScanner>();
builder.Services.AddSingleton<IOpportunityScorer, OpportunityScorer>();
builder.Services.AddSingleton<IArbitragerAddressBook, AddressBook>();
builder.Services.AddSingleton<IArrayBuilder, ArrayBuilder>();
builder.Services.AddSingleton<IArbitrageExecutor, ArbitrageExecutor>();

// ── Hosted services ───────────────────────────────────────────────────────────
// Resolve BlockControllerClient from DI so it gets the typed-client HttpClient
// (BaseAddress + Timeout) configured above via AddHttpClient<BlockControllerClient>.
builder.Services.AddHostedService(sp => sp.GetRequiredService<BlockControllerClient>());
builder.Services.AddHostedService<AddressBookStartupService>();
builder.Services.AddHostedService<ScannerWorker>();
builder.Services.AddHostedService<PriceFeedWorker>();
builder.Services.AddHostedService<ExecutorPipeline>();

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
app.MapHub<ArbitragerHub>("/hubs/arbitrager");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", module = "arbitrager" }));

// HTTP port 5400 + WebSocket/SignalR port 6400
app.Urls.Add("http://0.0.0.0:5400");
app.Urls.Add("http://0.0.0.0:6400");

await app.RunAsync();
