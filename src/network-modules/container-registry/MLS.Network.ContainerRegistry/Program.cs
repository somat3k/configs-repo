using MLS.Network.ContainerRegistry.Hubs;
using MLS.Network.ContainerRegistry.Models;
using MLS.Network.ContainerRegistry.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ContainerRegistryConfig>(
    builder.Configuration.GetSection("ContainerRegistry"));

var cfg = builder.Configuration.GetSection("ContainerRegistry").Get<ContainerRegistryConfig>()
          ?? new ContainerRegistryConfig();

try
{
    var redis = StackExchange.Redis.ConnectionMultiplexer.Connect(cfg.RedisConnectionString);
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(redis);
}
catch (Exception ex)
{
    Console.WriteLine($"[ContainerRegistry] Redis unavailable: {ex.Message}");
}

builder.Services.AddHttpClient<BlockControllerClient>(client =>
{
    client.BaseAddress = new Uri(cfg.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient();

builder.Services.AddSingleton<IContainerRegistryService, ContainerRegistryService>();
builder.Services.AddHostedService<HealthProbeService>();
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
app.MapHub<ContainerRegistryHub>("/hubs/container-registry");
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    module = ContainerRegistryConstants.ModuleName,
}));

app.Urls.Add($"http://0.0.0.0:{ContainerRegistryConstants.HttpPort}");
app.Urls.Add($"http://0.0.0.0:{ContainerRegistryConstants.WsPort}");

await app.RunAsync();
