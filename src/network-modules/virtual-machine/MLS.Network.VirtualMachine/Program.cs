using MLS.Network.VirtualMachine.Hubs;
using MLS.Network.VirtualMachine.Models;
using MLS.Network.VirtualMachine.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<VirtualMachineConfig>(
    builder.Configuration.GetSection("VirtualMachine"));

var cfg = builder.Configuration.GetSection("VirtualMachine").Get<VirtualMachineConfig>()
          ?? new VirtualMachineConfig();

try
{
    var redis = StackExchange.Redis.ConnectionMultiplexer.Connect(cfg.RedisConnectionString);
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(redis);
}
catch (Exception ex)
{
    Console.WriteLine($"[VirtualMachine] Redis unavailable: {ex.Message}");
}

builder.Services.AddHttpClient<BlockControllerClient>(client =>
{
    client.BaseAddress = new Uri(cfg.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<IVirtualMachineService, VirtualMachineService>();
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
app.MapHub<VirtualMachineHub>("/hubs/virtual-machine");
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    module = VirtualMachineConstants.ModuleName,
}));

app.Urls.Add($"http://0.0.0.0:{VirtualMachineConstants.HttpPort}");
app.Urls.Add($"http://0.0.0.0:{VirtualMachineConstants.WsPort}");

await app.RunAsync();
