using MLS.Transactions.Configuration;
using MLS.Transactions.Controllers;
using MLS.Transactions.Hubs;
using MLS.Transactions.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<TransactionsOptions>(builder.Configuration.GetSection("Transactions"));

var txOpts = builder.Configuration.GetSection("Transactions").Get<TransactionsOptions>()
             ?? new TransactionsOptions();

// ── HTTP clients ──────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<BlockControllerClient>(client =>
{
    client.BaseAddress = new Uri(txOpts.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<IEnvelopeSender, EnvelopeSender>(client =>
{
    client.BaseAddress = new Uri(txOpts.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

// ── Core services ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ModuleIdentity>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<BlockControllerClient>());

// ── ASP.NET Core ──────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Kestrel ───────────────────────────────────────────────────────────────────
builder.WebHost.UseUrls("http://0.0.0.0:5900");

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<TransactionsHub>("/hubs/transactions");

app.Logger.LogInformation("MLS.Transactions starting on http://0.0.0.0:5900 | hub=/hubs/transactions");

await app.RunAsync();
