using Microsoft.EntityFrameworkCore;
using MLS.Designer.Blocks.Arbitrage;
using MLS.Designer.Blocks.CustomTiles;
using MLS.Designer.Blocks.DataHydra;
using MLS.Designer.Blocks.DeFi;
using MLS.Designer.Blocks.MLTraining;
using MLS.Designer.Blocks.Trading.DataSourceBlocks;
using MLS.Designer.Blocks.Trading.ExecutionBlocks;
using MLS.Designer.Blocks.Trading.IndicatorBlocks;
using MLS.Designer.Blocks.Trading.MLBlocks;
using MLS.Designer.Blocks.Trading.RiskBlocks;
using MLS.Designer.Blocks.Trading.StrategyBlocks;
using MLS.Designer.Compilation;
using MLS.Designer.Configuration;
using MLS.Designer.Exchanges;
using MLS.Designer.Hubs;
using MLS.Designer.Persistence;
using MLS.Designer.Services;
using MLS.Core.Designer;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<DesignerOptions>(builder.Configuration.GetSection("Designer"));

// ── HTTP clients ──────────────────────────────────────────────────────────────
var designerOpts = builder.Configuration.GetSection("Designer").Get<DesignerOptions>()
                   ?? new DesignerOptions();

builder.Services
    .AddHttpClient<BlockControllerClient>(client =>
    {
        client.BaseAddress = new Uri(designerOpts.BlockControllerUrl);
        client.Timeout     = TimeSpan.FromSeconds(10);
    });

// ── Exchange adapter HTTP clients ─────────────────────────────────────────────
builder.Services.AddHttpClient<HyperliquidAdapter>()
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(15));

builder.Services.AddHttpClient<CamelotAdapter>()
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddHttpClient<DFYNAdapter>()
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddHttpClient<BalancerAdapter>()
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddHttpClient<MorphoAdapter>()
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));

// ── IPFS HTTP client (for Roslyn compiler + DynamicBlockLoader) ───────────────
var ipfsApiUrl = designerOpts.IpfsApiUrl?.Trim();
builder.Services.AddHttpClient("ipfs", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);

    if (string.IsNullOrWhiteSpace(ipfsApiUrl))
        return; // IPFS disabled — base address intentionally omitted

    var normalised = ipfsApiUrl.TrimEnd('/') + "/";
    if (!Uri.TryCreate(normalised, UriKind.Absolute, out var baseUri))
        throw new InvalidOperationException(
            $"Designer:IpfsApiUrl '{ipfsApiUrl}' must be a valid absolute URI or empty to disable IPFS operations.");

    client.BaseAddress = baseUri;
});

// ── PostgreSQL data source (owned by DI — adapters/registry must NOT dispose it) ──
var pgConnStr = designerOpts.PostgresConnectionString;
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(pgConnStr));

// ── Blockchain address book ───────────────────────────────────────────────────
builder.Services.AddSingleton<IBlockchainAddressBook, ExchangeRegistry>();
builder.Services.AddHostedService<ExchangeRegistryStartupService>();

// ── Exchange adapters (singleton — share the HTTP client factory cache) ───────
builder.Services.AddSingleton<HyperliquidAdapter>();
builder.Services.AddSingleton<CamelotAdapter>();
builder.Services.AddSingleton<DFYNAdapter>();
builder.Services.AddSingleton<BalancerAdapter>();
builder.Services.AddSingleton<MorphoAdapter>();

// ── EF Core — Designer DB context ────────────────────────────────────────────
builder.Services.AddDbContext<DesignerDbContext>(options =>
    options.UseNpgsql(designerOpts.PostgresConnectionString));
builder.Services.AddScoped<StrategyRepository>();

// ── Transformation controller + tile rule engine ──────────────────────────────
builder.Services.AddSingleton<ITransformationController, TransformationController>();
builder.Services.AddSingleton<TileRuleEngine>();

// ── Envelope sender (for StrategiesController → Block Controller) ─────────────
builder.Services.AddHttpClient<IEnvelopeSender, EnvelopeSender>(client =>
{
    client.BaseAddress = new Uri(designerOpts.BlockControllerUrl);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts => opts.JsonSerializerOptions.WriteIndented = false);

builder.Services.AddSignalR(hub =>
{
    hub.EnableDetailedErrors = builder.Environment.IsDevelopment();
    hub.MaximumReceiveMessageSize = 1024 * 1024;
});

// ── Training dispatcher ───────────────────────────────────────────────────────
builder.Services.AddSingleton<ITrainingDispatcher, TrainingDispatcher>();

// ── Roslyn compilation + dynamic block loading ────────────────────────────────
builder.Services.AddSingleton<IStrategyCompiler, RoslynStrategyCompiler>();
builder.Services.AddSingleton<DynamicBlockLoader>();

builder.Services.AddSingleton<IBlockRegistry>(sp =>
{
    var registry = new BlockRegistry();

    // ── DataSource blocks ──────────────────────────────────────────────────────
    registry.Register<CandleFeedBlock>("CandleFeedBlock");
    registry.Register<OrderBookFeedBlock>("OrderBookFeedBlock");
    registry.Register<TradeFeedBlock>("TradeFeedBlock");
    registry.Register<BacktestReplayBlock>("BacktestReplayBlock");

    // ── Indicator blocks ───────────────────────────────────────────────────────
    registry.Register<RSIBlock>("RSIBlock");
    registry.Register<MACDBlock>("MACDBlock");
    registry.Register<BollingerBlock>("BollingerBlock");
    registry.Register<ATRBlock>("ATRBlock");
    registry.Register<VWAPBlock>("VWAPBlock");
    registry.Register<VolumeProfileBlock>("VolumeProfileBlock");
    registry.Register<CustomIndicatorBlock>("CustomIndicatorBlock");

    // ── ML blocks ──────────────────────────────────────────────────────────────
    registry.Register<ModelTInferenceBlock>("ModelTInferenceBlock");
    registry.Register<ModelAInferenceBlock>("ModelAInferenceBlock");
    registry.Register<ModelDInferenceBlock>("ModelDInferenceBlock");
    registry.Register<EnsembleBlock>("EnsembleBlock");

    // ── Strategy blocks ────────────────────────────────────────────────────────
    registry.Register<MomentumStrategyBlock>("MomentumStrategyBlock");
    registry.Register<MeanReversionBlock>("MeanReversionBlock");
    registry.Register<TrendFollowBlock>("TrendFollowBlock");
    registry.Register<CompositeStrategyBlock>("CompositeStrategyBlock");

    // ── Risk blocks ────────────────────────────────────────────────────────────
    registry.Register<PositionSizerBlock>("PositionSizerBlock");
    registry.Register<StopLossBlock>("StopLossBlock");
    registry.Register<DrawdownGuardBlock>("DrawdownGuardBlock");
    registry.Register<ExposureLimitBlock>("ExposureLimitBlock");

    // ── Execution blocks ───────────────────────────────────────────────────────
    registry.Register<OrderEmitterBlock>("OrderEmitterBlock");
    registry.Register<OrderRouterBlock>("OrderRouterBlock");
    registry.Register<FillTrackerBlock>("FillTrackerBlock");
    registry.Register<SlippageEstimatorBlock>("SlippageEstimatorBlock");

    // ── Arbitrage blocks ───────────────────────────────────────────────────────
    registry.Register<SpreadCalculatorBlock>("SpreadCalculatorBlock");
    registry.Register<nHOPPathFinderBlock>("nHOPPathFinderBlock");
    registry.Register<FlashLoanBlock>("FlashLoanBlock");
    registry.Register<ProfitGateBlock>("ProfitGateBlock");

    // ── DeFi blocks ────────────────────────────────────────────────────────────
    registry.Register<MorphoSupplyBlock>("MorphoSupplyBlock");
    registry.Register<MorphoBorrowBlock>("MorphoBorrowBlock");
    registry.Register<BalancerSwapBlock>("BalancerSwapBlock");
    registry.Register<CollateralHealthBlock>("CollateralHealthBlock");
    registry.Register<YieldOptimizerBlock>("YieldOptimizerBlock");
    registry.Register<LiquidationGuardBlock>("LiquidationGuardBlock");
    registry.Register<LendingHealthBlock>("LendingHealthBlock");

    // ── ML Training blocks ─────────────────────────────────────────────────────
    var dispatcher = sp.GetRequiredService<ITrainingDispatcher>();
    registry.Register<DataLoaderBlock>("DataLoaderBlock");
    registry.Register<FeatureEngineerBlock>("FeatureEngineerBlock");
    registry.Register<TrainSplitBlock>("TrainSplitBlock");
    registry.Register("TrainModelBlock",      () => new TrainModelBlock(dispatcher));
    registry.Register<ValidateModelBlock>("ValidateModelBlock");
    registry.Register<ExportONNXBlock>("ExportONNXBlock");
    registry.Register("HyperparamSearchBlock", () => new HyperparamSearchBlock(dispatcher));

    // ── Custom Tile blocks ─────────────────────────────────────────────────────
    registry.Register<CustomIndicatorTile>("CustomIndicatorTile");
    registry.Register<PassThroughTile>("PassThroughTile");

    // ── Data Hydra blocks ──────────────────────────────────────────────────────
    registry.Register<FeedSourceBlock>("FeedSourceBlock");
    registry.Register<FilterBlock>("FilterBlock");
    registry.Register<NormalisationBlock>("NormalisationBlock");
    registry.Register<RouterBlock>("RouterBlock");
    registry.Register<BackfillBlock>("BackfillBlock");
    registry.Register<GapMonitorBlock>("GapMonitorBlock");

    return registry;
});

// MODULE_REGISTER + MODULE_HEARTBEAT background service
builder.Services.AddHostedService<BlockControllerClient>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MLS Designer", Version = "v1" });
});

builder.Logging.AddConsole();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<DesignerHub>("/hubs/designer");

// HTTP API port 5250 + WebSocket port 6250 — same Kestrel instance, both ports active
app.Urls.Add("http://0.0.0.0:5250");
app.Urls.Add("http://0.0.0.0:6250");

app.Run();

#pragma warning disable CS1591
public partial class Program { }
#pragma warning restore CS1591
