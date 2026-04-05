using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using MLS.AIHub.Canvas;
using MLS.AIHub.Configuration;
using MLS.AIHub.Plugins;
using Moq;
using Moq.Protected;
using System.Net.Http;
using Xunit;

namespace MLS.AIHub.Tests;

/// <summary>
/// Integration tests for the SK plugin pipeline using mock HTTP clients and a mock canvas dispatcher.
/// </summary>
public sealed class PluginPipelineTests
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static readonly IOptions<AIHubOptions> DefaultOptions =
        Options.Create(new AIHubOptions
        {
            TraderUrl    = "http://trader:5300",
            DesignerUrl  = "http://designer:5250",
            DeFiUrl      = "http://defi:5500",
            MlRuntimeUrl = "http://ml-runtime:5600",
        });

    // ── TradingPlugin ─────────────────────────────────────────────────────────

    [Fact]
    public async Task TradingPlugin_GetPositions_ReturnsParsedPositions()
    {
        var json = """
            [
              {"symbol":"BTC-PERP","side":"LONG","size":0.1,"entry_price":60000,"mark_price":62000,"unrealised_pnl":200.0,"leverage":5}
            ]
            """;

        var factory = BuildHttpFactory("http://trader:5300", HttpStatusCode.OK, json);
        var plugin  = new TradingPlugin(factory, DefaultOptions, NullLogger<TradingPlugin>.Instance);

        var result = await plugin.GetPositions();

        result.Should().Contain("BTC-PERP");
        result.Should().Contain("LONG");
        result.Should().Contain("+200");
    }

    [Fact]
    public async Task TradingPlugin_GetPositions_WithSymbolFilter_PassesQueryParam()
    {
        var json = "[]";
        HttpRequestMessage? capturedRequest = null;

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });

        var client  = new HttpClient(handler.Object) { BaseAddress = new Uri("http://trader:5300") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var plugin = new TradingPlugin(factory.Object, DefaultOptions, NullLogger<TradingPlugin>.Instance);

        var result = await plugin.GetPositions("BTC-PERP");

        result.Should().Contain("No open positions for BTC-PERP");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri.Should().NotBeNull();
        capturedRequest.RequestUri!.AbsolutePath.Should().Be("/api/positions");
        capturedRequest.RequestUri.Query.Should().Contain("symbol=BTC-PERP");
    }

    [Fact]
    public async Task TradingPlugin_PlaceOrder_RequiresConfirmation()
    {
        var factory = BuildHttpFactory("http://trader:5300", HttpStatusCode.OK, "{}");
        var plugin  = new TradingPlugin(factory, DefaultOptions, NullLogger<TradingPlugin>.Instance);

        var result = await plugin.PlaceOrder("BTC-PERP", "BUY", 0.01m, confirmed: false);

        result.Should().Contain("confirm");
        result.Should().Contain("BTC-PERP");
    }

    [Fact]
    public async Task TradingPlugin_PlaceOrder_ExecutesWhenConfirmed()
    {
        var json = """{"order_id":"ORD123","symbol":"BTC-PERP","status":"FILLED","filled_quantity":0.01}""";
        var factory = BuildHttpFactory("http://trader:5300", HttpStatusCode.OK, json);
        var plugin  = new TradingPlugin(factory, DefaultOptions, NullLogger<TradingPlugin>.Instance);

        var result = await plugin.PlaceOrder("BTC-PERP", "BUY", 0.01m, confirmed: true);

        result.Should().Contain("ORD123");
        result.Should().Contain("FILLED");
    }

    [Fact]
    public async Task TradingPlugin_GetPositions_ReturnsGracefulMessageOnError()
    {
        var factory = BuildHttpFactory("http://trader:5300", HttpStatusCode.ServiceUnavailable, "");
        var plugin  = new TradingPlugin(factory, DefaultOptions, NullLogger<TradingPlugin>.Instance);

        var result = await plugin.GetPositions();

        result.Should().Contain("Unable to retrieve positions");
    }

    [Fact]
    public async Task TradingPlugin_GetSignalHistory_ReturnsFormattedSignals()
    {
        var json = """
            [
              {"symbol":"ETH-PERP","direction":"BUY","confidence":0.82,"model_type":"Trading","timestamp":"2024-01-15T10:00:00Z"},
              {"symbol":"ETH-PERP","direction":"HOLD","confidence":0.61,"model_type":"Trading","timestamp":"2024-01-15T09:00:00Z"}
            ]
            """;
        var factory = BuildHttpFactory("http://trader:5300", HttpStatusCode.OK, json);
        var plugin  = new TradingPlugin(factory, DefaultOptions, NullLogger<TradingPlugin>.Instance);

        var result = await plugin.GetSignalHistory("ETH-PERP", 2);

        result.Should().Contain("BUY");
        result.Should().Contain("HOLD");
        result.Should().Contain("82%");
    }

    // ── DesignerPlugin ────────────────────────────────────────────────────────

    [Fact]
    public async Task DesignerPlugin_CreateStrategy_DispatchesCanvasActionAndReturnsId()
    {
        var json = """{"strategy_id":"11111111-1111-1111-1111-111111111111","name":"My RSI","state":"Stopped","block_count":4}""";
        var factory    = BuildHttpFactory("http://designer:5250", HttpStatusCode.OK, json);
        var dispatcher = new Mock<ICanvasActionDispatcher>();
        dispatcher.Setup(d => d.DispatchAsync(It.IsAny<CanvasAction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        var plugin = new DesignerPlugin(factory, DefaultOptions, dispatcher.Object, NullLogger<DesignerPlugin>.Instance);

        var result = await plugin.CreateStrategy(TestUserId, "My RSI", "rsi-crossover");

        result.Should().Contain("My RSI");
        result.Should().Contain("11111111-1111-1111-1111-111111111111");
        dispatcher.Verify(d => d.DispatchAsync(
            It.IsAny<OpenDesignerGraphAction>(),
            TestUserId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DesignerPlugin_CreateStrategy_RejectsEmptyUserId()
    {
        var factory    = BuildHttpFactory("http://designer:5250", HttpStatusCode.OK, "{}");
        var dispatcher = Mock.Of<ICanvasActionDispatcher>();
        var plugin     = new DesignerPlugin(factory, DefaultOptions, dispatcher, NullLogger<DesignerPlugin>.Instance);

        var result = await plugin.CreateStrategy(Guid.Empty, "My RSI", "rsi-crossover");

        result.Should().Contain("valid userId");
    }

    [Fact]
    public async Task DesignerPlugin_RunBacktest_RejectsInvalidDateRange()
    {
        var factory    = BuildHttpFactory("http://designer:5250", HttpStatusCode.OK, "{}");
        var dispatcher = Mock.Of<ICanvasActionDispatcher>();
        var plugin     = new DesignerPlugin(factory, DefaultOptions, dispatcher, NullLogger<DesignerPlugin>.Instance);

        var from = DateTimeOffset.UtcNow;
        var to   = from.AddDays(-1); // to < from — invalid

        var result = await plugin.RunBacktest(TestUserId, Guid.NewGuid(), from, to);

        result.Should().Contain("before");
    }

    // ── AnalyticsPlugin ───────────────────────────────────────────────────────

    [Fact]
    public async Task AnalyticsPlugin_PlotChart_DispatchesOpenPanelAction()
    {
        var factory    = BuildHttpFactory("http://trader:5300", HttpStatusCode.OK, "[]");
        var dispatcher = new Mock<ICanvasActionDispatcher>();
        dispatcher.Setup(d => d.DispatchAsync(It.IsAny<CanvasAction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        var plugin = new AnalyticsPlugin(factory, DefaultOptions, dispatcher.Object, NullLogger<AnalyticsPlugin>.Instance);

        var result = await plugin.PlotChart(TestUserId, "BTC-PERP", "4h");

        result.Should().Contain("BTC-PERP");
        result.Should().Contain("4H");
        dispatcher.Verify(d => d.DispatchAsync(
            It.Is<OpenPanelAction>(a => a.PanelType == "TradingChart"),
            TestUserId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyticsPlugin_PlotChart_RejectsEmptyUserId()
    {
        var factory    = BuildHttpFactory("http://trader:5300", HttpStatusCode.OK, "[]");
        var dispatcher = Mock.Of<ICanvasActionDispatcher>();
        var plugin     = new AnalyticsPlugin(factory, DefaultOptions, dispatcher, NullLogger<AnalyticsPlugin>.Instance);

        var result = await plugin.PlotChart(Guid.Empty, "BTC-PERP");

        result.Should().Contain("valid userId");
    }

    // ── MLRuntimePlugin ───────────────────────────────────────────────────────

    [Fact]
    public async Task MLRuntimePlugin_TrainModel_RequiresConfirmation()
    {
        var factory    = BuildHttpFactory("http://ml-runtime:5600", HttpStatusCode.OK, "{}");
        var dispatcher = Mock.Of<ICanvasActionDispatcher>();
        var plugin     = new MLRuntimePlugin(factory, DefaultOptions, dispatcher, NullLogger<MLRuntimePlugin>.Instance);

        var result = await plugin.TrainModel("trading", confirmed: false);

        result.Should().Contain("confirm");
    }

    [Fact]
    public async Task MLRuntimePlugin_DeployModel_RequiresConfirmation()
    {
        var factory    = BuildHttpFactory("http://ml-runtime:5600", HttpStatusCode.OK, "{}");
        var dispatcher = Mock.Of<ICanvasActionDispatcher>();
        var plugin     = new MLRuntimePlugin(factory, DefaultOptions, dispatcher, NullLogger<MLRuntimePlugin>.Instance);

        var result = await plugin.DeployModel("model-t", confirmed: false);

        result.Should().Contain("confirm");
    }

    [Fact]
    public async Task MLRuntimePlugin_ListModels_FormatsOutput()
    {
        var json = """
            [
              {"model_id":"model-t","model_type":"Trading","state":"Complete","accuracy":0.85,"last_trained":"2024-01-01T00:00:00Z"},
              {"model_id":"model-a","model_type":"Arbitrage","state":"Training","accuracy":0.0,"last_trained":null}
            ]
            """;
        var factory    = BuildHttpFactory("http://ml-runtime:5600", HttpStatusCode.OK, json);
        var dispatcher = Mock.Of<ICanvasActionDispatcher>();
        var plugin     = new MLRuntimePlugin(factory, DefaultOptions, dispatcher, NullLogger<MLRuntimePlugin>.Instance);

        var result = await plugin.ListModels();

        result.Should().Contain("model-t");
        result.Should().Contain("model-a");
        result.Should().Contain("85.0%");
    }

    // ── DeFiPlugin ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeFiPlugin_GetHealthFactors_HighlightsRiskyPositions()
    {
        var json = """
            [
              {"position_id":"pos1","protocol":"Morpho","health_factor":1.05,"collateral_usd":10000,"borrow_usd":8000,"severity":"Critical"},
              {"position_id":"pos2","protocol":"Balancer","health_factor":2.5,"collateral_usd":5000,"borrow_usd":1000,"severity":"Healthy"}
            ]
            """;
        var factory    = BuildHttpFactory("http://defi:5500", HttpStatusCode.OK, json);
        var dispatcher = Mock.Of<ICanvasActionDispatcher>();
        var plugin     = new DeFiPlugin(factory, DefaultOptions, dispatcher, NullLogger<DeFiPlugin>.Instance);

        var result = await plugin.GetHealthFactors();

        result.Should().Contain("AT RISK");
        result.Should().Contain("Morpho");
        result.Should().Contain("Balancer");
    }

    [Fact]
    public async Task DeFiPlugin_SimulateRebalance_RejectsInvalidJson()
    {
        var factory    = BuildHttpFactory("http://defi:5500", HttpStatusCode.OK, "{}");
        var dispatcher = Mock.Of<ICanvasActionDispatcher>();
        var plugin     = new DeFiPlugin(factory, DefaultOptions, dispatcher, NullLogger<DeFiPlugin>.Instance);

        var result = await plugin.SimulateRebalance(TestUserId, "not-valid-json{{{");

        result.Should().Contain("Invalid JSON");
    }

    [Fact]
    public async Task DeFiPlugin_GetPoolAPYs_FormatsOutput()
    {
        var json = """
            [
              {"protocol":"Balancer","pool_name":"WETH/USDC 0.05%","apy_percent":8.5,"tvl_usd":1500000},
              {"protocol":"Morpho","pool_name":"WETH Supply Market","apy_percent":4.2,"tvl_usd":3000000}
            ]
            """;
        var factory    = BuildHttpFactory("http://defi:5500", HttpStatusCode.OK, json);
        var dispatcher = Mock.Of<ICanvasActionDispatcher>();
        var plugin     = new DeFiPlugin(factory, DefaultOptions, dispatcher, NullLogger<DeFiPlugin>.Instance);

        var result = await plugin.GetPoolAPYs();

        result.Should().Contain("Balancer");
        result.Should().Contain("8.50%");
        result.Should().Contain("Morpho");
    }

    // ── Canvas types ──────────────────────────────────────────────────────────

    [Fact]
    public void CanvasAction_ActionType_ReturnsCorrectDiscriminator()
    {
        var data      = JsonSerializer.SerializeToElement(new { });
        var open      = new OpenPanelAction("TradingChart", data);
        var highlight = new HighlightBlockAction(Guid.NewGuid(), "#00d4ff");
        var diagram   = new ShowDiagramAction("graph TD; A-->B", "Test");

        open.ActionType.Should().Be("OpenPanel");
        highlight.ActionType.Should().Be("HighlightBlock");
        diagram.ActionType.Should().Be("ShowDiagram");
    }

    // ── Kernel-level integration test ─────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="TradingPlugin"/> is properly discoverable and invocable via
    /// the Semantic Kernel plugin system — confirming all <c>[KernelFunction]</c> / <c>[Description]</c>
    /// attributes are valid for AI tool discovery.
    /// </summary>
    [Fact]
    public async Task Kernel_InvokePlugin_GetPositions_ReturnsExpectedOutput()
    {
        // Arrange — build a kernel and register the plugin via SK's plugin system
        var json    = """[{"symbol":"ETH-PERP","side":"SHORT","size":1.0,"entry_price":3000,"mark_price":2900,"unrealised_pnl":100.0,"leverage":2}]""";
        var factory = BuildHttpFactory("http://trader:5300", HttpStatusCode.OK, json);

        var plugin = new TradingPlugin(factory, DefaultOptions, NullLogger<TradingPlugin>.Instance);

        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(plugin, "Trading");

        // Act — invoke through the kernel's function invocation pipeline
        var function = kernel.Plugins["Trading"]["GetPositions"];
        var result   = await kernel.InvokeAsync(function, new KernelArguments());

        // Assert — plugin is correctly invokable through the kernel
        var output = result.GetValue<string>();
        output.Should().NotBeNull();
        output.Should().Contain("ETH-PERP");
        output.Should().Contain("SHORT");
    }

    [Fact]
    public void Kernel_TradingPlugin_ExposesExpectedFunctions()
    {
        // Arrange
        var factory = BuildHttpFactory("http://trader:5300", HttpStatusCode.OK, "[]");
        var plugin  = new TradingPlugin(factory, DefaultOptions, NullLogger<TradingPlugin>.Instance);

        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(plugin, "Trading");

        // Assert — all declared KernelFunctions are discoverable
        var functions = kernel.Plugins["Trading"].GetFunctionsMetadata();
        var names     = functions.Select(f => f.Name).ToList();

        names.Should().Contain("GetPositions");
        names.Should().Contain("PlaceOrder");
        names.Should().Contain("GetSignalHistory");
        names.Should().Contain("GetPnLSummary");
    }

    [Fact]
    public void Kernel_AnalyticsPlugin_ExposesExpectedFunctions()
    {
        var factory    = BuildHttpFactory("http://trader:5300", HttpStatusCode.OK, "[]");
        var dispatcher = Mock.Of<ICanvasActionDispatcher>();
        var plugin     = new AnalyticsPlugin(factory, DefaultOptions, dispatcher, NullLogger<AnalyticsPlugin>.Instance);

        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.AddFromObject(plugin, "Analytics");

        var names = kernel.Plugins["Analytics"].GetFunctionsMetadata().Select(f => f.Name).ToList();

        names.Should().Contain("PlotChart");
        names.Should().Contain("GenerateSHAP");
        names.Should().Contain("ExportReport");
        names.Should().Contain("AskAboutData");
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    private static IHttpClientFactory BuildHttpFactory(
        string baseUrl, HttpStatusCode status, string responseBody)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json"),
            });

        var client  = new HttpClient(handler.Object) { BaseAddress = new Uri(baseUrl) };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        return factory.Object;
    }
}
