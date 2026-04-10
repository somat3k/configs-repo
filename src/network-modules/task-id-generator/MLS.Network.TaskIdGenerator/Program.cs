using MLS.Network.TaskIdGenerator.Hubs;
using MLS.Network.TaskIdGenerator.Models;
using MLS.Network.TaskIdGenerator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TaskIdGeneratorConfig>(
    builder.Configuration.GetSection("TaskIdGenerator"));

var cfg = builder.Configuration.GetSection("TaskIdGenerator").Get<TaskIdGeneratorConfig>()
          ?? new TaskIdGeneratorConfig();

try
{
    var redis = StackExchange.Redis.ConnectionMultiplexer.Connect(cfg.RedisConnectionString);
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(redis);
}
catch (Exception ex)
{
    Console.WriteLine($"[TaskIdGenerator] Redis unavailable: {ex.Message}");
}

builder.Services.AddHttpClient<BlockControllerClient>(client =>
{
    client.BaseAddress = new Uri(cfg.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<ITaskIdService, TaskIdService>();
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
app.MapHub<TaskIdGeneratorHub>("/hubs/task-id-generator");
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    module = TaskIdGeneratorConstants.ModuleName,
}));

app.Urls.Add($"http://0.0.0.0:{TaskIdGeneratorConstants.HttpPort}");
app.Urls.Add($"http://0.0.0.0:{TaskIdGeneratorConstants.WsPort}");

await app.RunAsync();
