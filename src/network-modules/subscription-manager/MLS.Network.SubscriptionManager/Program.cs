using MLS.Network.SubscriptionManager.Hubs;
using MLS.Network.SubscriptionManager.Models;
using MLS.Network.SubscriptionManager.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SubscriptionManagerConfig>(
    builder.Configuration.GetSection("SubscriptionManager"));

var cfg = builder.Configuration.GetSection("SubscriptionManager").Get<SubscriptionManagerConfig>()
          ?? new SubscriptionManagerConfig();

try
{
    var redis = StackExchange.Redis.ConnectionMultiplexer.Connect(cfg.RedisConnectionString);
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(redis);
}
catch (Exception ex)
{
    Console.WriteLine($"[SubscriptionManager] Redis unavailable: {ex.Message}");
}

builder.Services.AddHttpClient<BlockControllerClient>(client =>
{
    client.BaseAddress = new Uri(cfg.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

builder.Services.AddSignalR();

// SubscriptionService needs IHubContext — register after SignalR
builder.Services.AddSingleton<ISubscriptionService>(sp => new SubscriptionService(
    sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<SubscriptionManagerHub>>(),
    sp.GetRequiredService<ILogger<SubscriptionService>>()));

builder.Services.AddHostedService(sp => sp.GetRequiredService<BlockControllerClient>());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<SubscriptionManagerHub>("/hubs/subscription-manager");
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    module = SubscriptionManagerConstants.ModuleName,
}));

app.Urls.Add($"http://0.0.0.0:{SubscriptionManagerConstants.HttpPort}");
app.Urls.Add($"http://0.0.0.0:{SubscriptionManagerConstants.WsPort}");

await app.RunAsync();
