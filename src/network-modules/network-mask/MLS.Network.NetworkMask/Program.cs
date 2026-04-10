using MLS.Network.NetworkMask.Hubs;
using MLS.Network.NetworkMask.Models;
using MLS.Network.NetworkMask.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<NetworkMaskConfig>(
    builder.Configuration.GetSection("NetworkMask"));

var cfg = builder.Configuration.GetSection("NetworkMask").Get<NetworkMaskConfig>()
          ?? new NetworkMaskConfig();

try
{
    var redis = StackExchange.Redis.ConnectionMultiplexer.Connect(cfg.RedisConnectionString);
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(redis);
}
catch (Exception ex)
{
    Console.WriteLine($"[NetworkMask] Redis unavailable: {ex.Message}");
}

builder.Services.AddHttpClient<BlockControllerClient>(client =>
{
    client.BaseAddress = new Uri(cfg.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<INetworkMaskService, NetworkMaskService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<BlockControllerClient>());

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
app.MapHub<NetworkMaskHub>("/hubs/network-mask");
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    module = NetworkMaskConstants.ModuleName,
}));

app.Urls.Add($"http://0.0.0.0:{NetworkMaskConstants.HttpPort}");
app.Urls.Add($"http://0.0.0.0:{NetworkMaskConstants.WsPort}");

await app.RunAsync();
