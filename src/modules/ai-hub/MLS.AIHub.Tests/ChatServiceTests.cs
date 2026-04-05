using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MLS.AIHub.Canvas;
using MLS.AIHub.Configuration;
using MLS.AIHub.Context;
using MLS.AIHub.Hubs;
using MLS.AIHub.Plugins;
using MLS.AIHub.Providers;
using MLS.AIHub.Services;
using MLS.Core.Contracts.Designer;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;
using AIHubHub = MLS.AIHub.Hubs.AIHub;

namespace MLS.AIHub.Tests;

/// <summary>
/// Integration tests for <see cref="ChatService"/> verifying the end-to-end pipeline:
/// context assembly → provider selection → SK streaming → SignalR chunk delivery.
/// Uses mock SK kernel and mock provider per the Session 10 acceptance criteria.
/// </summary>
public sealed class ChatServiceTests
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IHttpClientFactory BuildEmptyHttpFactory() =>
        BuildHttpFactory(HttpStatusCode.OK, "[]");

    private static IHttpClientFactory BuildHttpFactory(HttpStatusCode status, string body)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        var client  = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        return factory.Object;
    }

    /// <summary>
    /// Creates a mock <see cref="IChatCompletionService"/> that returns a fixed sequence of chunks.
    /// </summary>
    private static IChatCompletionService BuildMockChatService(params string[] chunks)
    {
        var mock = new Mock<IChatCompletionService>();

        mock.Setup(s => s.GetStreamingChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(chunks));

        return mock.Object;
    }

    private static async IAsyncEnumerable<StreamingChatMessageContent> ToAsyncEnumerable(
        IEnumerable<string> texts)
    {
        foreach (var text in texts)
        {
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, text);
            await Task.Yield();
        }
    }

    private static Mock<ILLMProvider> BuildMockProvider(IChatCompletionService chatSvc)
    {
        var mock = new Mock<ILLMProvider>();
        mock.SetupGet(p => p.ProviderId).Returns("mock-provider");
        mock.SetupGet(p => p.DisplayName).Returns("Mock Provider");
        mock.SetupGet(p => p.SupportedModels).Returns(["mock-model"]);
        mock.SetupGet(p => p.IsAvailable).Returns(true);
        mock.Setup(p => p.BuildService(It.IsAny<string>())).Returns(chatSvc);
        return mock;
    }

    private static Mock<IProviderRouter> BuildMockRouter(ILLMProvider provider)
    {
        var mock = new Mock<IProviderRouter>();
        mock.Setup(r => r.SelectProviderAsync(
                It.IsAny<AiQueryPayload>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider);
        return mock;
    }

    private static Mock<IContextAssembler> BuildMockContextAssembler()
    {
        var mock = new Mock<IContextAssembler>();
        mock.Setup(c => c.AssembleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectSnapshot
            {
                AssembledAt      = DateTimeOffset.UtcNow,
                AssemblyMs       = 42,
                Modules          = [],
                OpenPositions    = [],
                RecentSignals    = [],
                ArbOpportunities = [],
                DefiHealth       = [],
                MlModels         = [],
                ActiveStrategies = [],
                EnvelopeHistory  = [],
                FailedSources    = new List<string>().AsReadOnly(),
            });
        return mock;
    }

    /// <summary>
    /// Captures all envelopes sent through a mocked <see cref="IHubContext{AIHub}"/>.
    /// </summary>
    private static (Mock<IHubContext<AIHubHub>> HubMock, List<object?[]> Calls) BuildMockHub()
    {
        var calls      = new List<object?[]>();
        var clientProxy = new Mock<IClientProxy>();

        clientProxy
            .Setup(c => c.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((_, args, _) => calls.Add(args))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxy.Object);

        var hub = new Mock<IHubContext<AIHubHub>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);

        return (hub, calls);
    }

    private static ChatService BuildChatService(
        IContextAssembler contextAssembler,
        IProviderRouter router,
        IHubContext<AIHubHub> hub,
        IHttpClientFactory? httpFactory = null)
    {
        var factory    = httpFactory ?? BuildEmptyHttpFactory();
        var dispatcher = Mock.Of<ICanvasActionDispatcher>();

        return new ChatService(
            contextAssembler,
            router,
            hub,
            new TradingPlugin(factory, DefaultOptions, NullLogger<TradingPlugin>.Instance),
            new DesignerPlugin(factory, DefaultOptions, dispatcher, NullLogger<DesignerPlugin>.Instance),
            new AnalyticsPlugin(factory, DefaultOptions, dispatcher, NullLogger<AnalyticsPlugin>.Instance),
            new MLRuntimePlugin(factory, DefaultOptions, dispatcher, NullLogger<MLRuntimePlugin>.Instance),
            new DeFiPlugin(factory, DefaultOptions, dispatcher, NullLogger<DeFiPlugin>.Instance),
            NullLogger<ChatService>.Instance);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessQueryAsync_SendsEachChunkAsAiResponseChunk()
    {
        // Arrange
        var chatSvc  = BuildMockChatService("Hello", " world", "!");
        var provider = BuildMockProvider(chatSvc);
        var router   = BuildMockRouter(provider.Object);
        var context  = BuildMockContextAssembler();
        var (hub, calls) = BuildMockHub();

        var service  = BuildChatService(context.Object, router.Object, hub.Object);
        var query    = BuildQuery("Say hello");

        // Act
        await service.ProcessQueryAsync(query, TestUserId);

        // Assert — 3 text chunks + 1 AI_RESPONSE_COMPLETE = 4 total SendAsync calls
        calls.Should().HaveCount(4);
    }

    [Fact]
    public async Task ProcessQueryAsync_SendsAiResponseCompleteAsLastEnvelope()
    {
        // Arrange
        var chatSvc  = BuildMockChatService("Token1", "Token2");
        var provider = BuildMockProvider(chatSvc);
        var router   = BuildMockRouter(provider.Object);
        var (hub, calls) = BuildMockHub();

        var service = BuildChatService(BuildMockContextAssembler().Object, router.Object, hub.Object);
        var query   = BuildQuery("test");

        // Act
        await service.ProcessQueryAsync(query, TestUserId);

        // Assert — last call must be AI_RESPONSE_COMPLETE
        calls.Should().NotBeEmpty();
        var lastEnvelope = calls.Last()[0] as MLS.Core.Contracts.EnvelopePayload;
        lastEnvelope.Should().NotBeNull();
        lastEnvelope!.Type.Should().Be(MLS.Core.Constants.MessageTypes.AiResponseComplete);
    }

    [Fact]
    public async Task ProcessQueryAsync_ChunkIndexIsSequential()
    {
        // Arrange
        var chatSvc  = BuildMockChatService("A", "B", "C");
        var provider = BuildMockProvider(chatSvc);
        var router   = BuildMockRouter(provider.Object);
        var (hub, calls) = BuildMockHub();

        var service = BuildChatService(BuildMockContextAssembler().Object, router.Object, hub.Object);

        // Act
        await service.ProcessQueryAsync(BuildQuery("test"), TestUserId);

        // Assert — chunk indices 0, 1, 2, then complete payload with TotalChunks=3
        var chunkCalls = calls.Take(3).ToList();
        for (int i = 0; i < 3; i++)
        {
            var env = chunkCalls[i][0] as MLS.Core.Contracts.EnvelopePayload;
            env.Should().NotBeNull();
            env!.Type.Should().Be(MLS.Core.Constants.MessageTypes.AiResponseChunk);

            var payload = JsonSerializer.Deserialize<AiResponseChunkPayload>(
                env.Payload.GetRawText());
            payload!.ChunkIndex.Should().Be(i);
            payload.Text.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task ProcessQueryAsync_CompletePayloadHasCorrectTotalChunks()
    {
        // Arrange
        var chatSvc  = BuildMockChatService("one", "two", "three", "four");
        var provider = BuildMockProvider(chatSvc);
        var router   = BuildMockRouter(provider.Object);
        var (hub, calls) = BuildMockHub();

        var service = BuildChatService(BuildMockContextAssembler().Object, router.Object, hub.Object);

        // Act
        await service.ProcessQueryAsync(BuildQuery("count"), TestUserId);

        // Assert
        var completeEnv = calls.Last()[0] as MLS.Core.Contracts.EnvelopePayload;
        var completePayload = JsonSerializer.Deserialize<AiResponseCompletePayload>(
            completeEnv!.Payload.GetRawText());

        completePayload.Should().NotBeNull();
        completePayload!.TotalChunks.Should().Be(4);
        completePayload.ProviderId.Should().Be("mock-provider");
        completePayload.ModelId.Should().Be("mock-model");
    }

    [Fact]
    public async Task ProcessQueryAsync_EmptyChunksFromProvider_SendsOnlyCompleteEnvelope()
    {
        // Arrange — provider returns no content
        var chatSvc  = BuildMockChatService();  // zero chunks
        var provider = BuildMockProvider(chatSvc);
        var router   = BuildMockRouter(provider.Object);
        var (hub, calls) = BuildMockHub();

        var service = BuildChatService(BuildMockContextAssembler().Object, router.Object, hub.Object);

        // Act
        await service.ProcessQueryAsync(BuildQuery("empty"), TestUserId);

        // Assert — only the AI_RESPONSE_COMPLETE envelope should have been sent
        calls.Should().HaveCount(1);
        var env = calls[0][0] as MLS.Core.Contracts.EnvelopePayload;
        env!.Type.Should().Be(MLS.Core.Constants.MessageTypes.AiResponseComplete);
    }

    [Fact]
    public async Task ProcessQueryAsync_RoutesToProviderSelectedByRouter()
    {
        // Arrange — track which provider was asked to BuildService
        var chatSvc  = BuildMockChatService("hi");
        var provider = BuildMockProvider(chatSvc);
        var router   = BuildMockRouter(provider.Object);
        var (hub, _) = BuildMockHub();

        var service = BuildChatService(BuildMockContextAssembler().Object, router.Object, hub.Object);

        // Act
        await service.ProcessQueryAsync(BuildQuery("route test"), TestUserId);

        // Assert — the router was called exactly once and the provider's BuildService was called
        router.Verify(r => r.SelectProviderAsync(
            It.IsAny<AiQueryPayload>(), TestUserId, It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(p => p.BuildService("mock-model"), Times.Once);
    }

    [Fact]
    public async Task StreamChunksAsync_YieldsTextChunks()
    {
        // Arrange
        var chatSvc  = BuildMockChatService("Hello", " streaming", " world");
        var provider = BuildMockProvider(chatSvc);
        var router   = BuildMockRouter(provider.Object);
        var (hub, _) = BuildMockHub();

        var service = BuildChatService(BuildMockContextAssembler().Object, router.Object, hub.Object);
        var query   = BuildQuery("stream test");

        // Act
        var chunks = new List<AiResponseChunkPayload>();
        await foreach (var chunk in service.StreamChunksAsync(query, TestUserId))
            chunks.Add(chunk);

        // Assert — 3 text chunks + 1 terminal (IsFinal=true)
        chunks.Should().HaveCount(4);
        chunks.Take(3).Should().AllSatisfy(c => c.IsFinal.Should().BeFalse());
        chunks.Last().IsFinal.Should().BeTrue();
    }

    [Fact]
    public async Task StreamChunksAsync_FinalChunkHasEmptyText()
    {
        // Arrange
        var chatSvc  = BuildMockChatService("chunk");
        var provider = BuildMockProvider(chatSvc);
        var router   = BuildMockRouter(provider.Object);
        var (hub, _) = BuildMockHub();

        var service = BuildChatService(BuildMockContextAssembler().Object, router.Object, hub.Object);

        // Act
        var chunks = new List<AiResponseChunkPayload>();
        await foreach (var chunk in service.StreamChunksAsync(BuildQuery("q"), TestUserId))
            chunks.Add(chunk);

        var finalChunk = chunks.Last();
        finalChunk.IsFinal.Should().BeTrue();
        finalChunk.Text.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessQueryAsync_ContextAssemblerCalledWithCorrectUserId()
    {
        // Arrange
        var chatSvc  = BuildMockChatService("ok");
        var provider = BuildMockProvider(chatSvc);
        var router   = BuildMockRouter(provider.Object);
        var context  = BuildMockContextAssembler();
        var (hub, _) = BuildMockHub();

        var service = BuildChatService(context.Object, router.Object, hub.Object);

        // Act
        await service.ProcessQueryAsync(BuildQuery("test"), TestUserId);

        // Assert
        context.Verify(c => c.AssembleAsync(TestUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessQueryAsync_SendsToCorrectUserGroup()
    {
        // Arrange
        var chatSvc  = BuildMockChatService("chunk");
        var provider = BuildMockProvider(chatSvc);
        var router   = BuildMockRouter(provider.Object);

        var capturedGroup = string.Empty;

        var clientProxy = new Mock<IClientProxy>();
        clientProxy
            .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>()))
            .Callback<string>(g => capturedGroup = g)
            .Returns(clientProxy.Object);

        var hub = new Mock<IHubContext<AIHubHub>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);

        var service = BuildChatService(BuildMockContextAssembler().Object, router.Object, hub.Object);

        // Act
        await service.ProcessQueryAsync(BuildQuery("group test"), TestUserId);

        // Assert — all messages routed to the userId's group
        capturedGroup.Should().Be(TestUserId.ToString());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AiQueryPayload BuildQuery(string text) => new(
        Query:                text,
        ProviderOverride:     null,
        ModelOverride:        null,
        IncludeCanvasContext: false,
        ConversationHistory:  []);
}
