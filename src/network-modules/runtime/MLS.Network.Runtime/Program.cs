using MLS.Network.Runtime.Hubs;
using MLS.Network.Runtime.Models;
using MLS.Network.Runtime.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RuntimeConfig>(builder.Configuration.GetSection("Runtime"));

var cfg = builder.Configuration.GetSection("Runtime").Get<RuntimeConfig>()
          ?? new RuntimeConfig();

try
{
    var redis = StackExchange.Redis.ConnectionMultiplexer.Connect(cfg.RedisConnectionString);
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(redis);
}
catch (Exception ex)
{
    Console.WriteLine($"[Runtime] Redis unavailable: {ex.Message}");
}

builder.Services.AddHttpClient<BlockControllerClient>(client =>
{
    client.BaseAddress = new Uri(cfg.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<IDockerClientFacade>(sp =>
{
    var options = sp.GetRequiredService<IOptions<RuntimeConfig>>().Value;
    try
    {
        var dockerClient = new Docker.DotNet.DockerClientConfiguration(
            new Uri(options.DockerSocketPath)).CreateClient();
        return new DockerClientFacade(dockerClient);
    }
    catch (Exception ex)
    {
        sp.GetRequiredService<ILogger<DockerClientFacade>>()
          .LogWarning(ex, "Docker socket unavailable — container operations will return NotFound");
        var fallback = new Docker.DotNet.DockerClientConfiguration(
            new Uri("unix:///var/run/docker.sock")).CreateClient();
        return new DockerClientFacade(fallback);
    }
});
builder.Services.AddSingleton<IModuleRuntimeService, ModuleRuntimeService>();
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
app.MapHub<RuntimeHub>("/hubs/runtime");
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    module = RuntimeConstants.ModuleName,
}));

app.Urls.Add($"http://0.0.0.0:{RuntimeConstants.HttpPort}");
app.Urls.Add($"http://0.0.0.0:{RuntimeConstants.WsPort}");

await app.RunAsync();
