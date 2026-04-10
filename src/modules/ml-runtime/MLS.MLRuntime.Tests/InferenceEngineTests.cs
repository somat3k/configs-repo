using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Moq;
using MLS.MLRuntime.Configuration;
using MLS.MLRuntime.Inference;
using MLS.MLRuntime.Models;
using StackExchange.Redis;
using Xunit;

namespace MLS.MLRuntime.Tests;

/// <summary>
/// Unit tests for <see cref="InferenceEngine"/>.
/// </summary>
public sealed class InferenceEngineTests
{
    private static IOptions<MLRuntimeOptions> DefaultOptions(int timeoutMs = 5000) =>
        Options.Create(new MLRuntimeOptions
        {
            InferenceTimeoutMs  = timeoutMs,
            RedisCacheTtlSeconds = 30,
        });

    private static InferenceRequestPayload MakeRequest(string modelKey = "model-t") =>
        new(
            RequestId:         Guid.NewGuid(),
            ModelKey:          modelKey,
            Features:          [1f, 2f, 3f],
            RequesterModuleId: "test");

    // ── Model not loaded ──────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ThrowsInvalidOperationException_WhenModelNotLoaded()
    {
        var registryMock = new Mock<IModelRegistry>();
        registryMock.Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ModelRecord?)null);

        var engine = new InferenceEngine(
            registryMock.Object, null, DefaultOptions(), NullLogger<InferenceEngine>.Instance);

        var act = () => engine.RunAsync(MakeRequest()).AsTask();
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*not loaded*");
    }

    // ── Redis cache miss → run inference ─────────────────────────────────────

    [Fact]
    public async Task RunAsync_ReturnsResult_WithCachedFalse_OnCacheMiss()
    {
        // Registry returns null (model not loaded) — we just want to verify
        // that when the cache misses, we proceed to model lookup.
        var registryMock = new Mock<IModelRegistry>();
        registryMock.Setup(r => r.GetAsync("model-t", It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ModelRecord?)null);

        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock    = new Mock<IDatabase>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                 .Returns(dbMock.Object);
        dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
              .ReturnsAsync(RedisValue.Null);

        var engine = new InferenceEngine(
            registryMock.Object, redisMock.Object, DefaultOptions(),
            NullLogger<InferenceEngine>.Instance);

        // Should throw because model not loaded — but proves Redis GET was tried
        var act = () => engine.RunAsync(MakeRequest()).AsTask();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Redis cache hit ───────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ReturnsCachedResult_WhenCacheHit()
    {
        var requestId = Guid.NewGuid();
        var cached    = new InferenceResultPayload(requestId, "model-t", [0.9f], 5.0, false, "v1");
        var json      = System.Text.Json.JsonSerializer.Serialize(cached);

        // The engine now resolves the model FIRST (to build a versioned cache key),
        // so we must provide a non-null ModelRecord even for a cache-hit test.
        var fakeSession  = CreateFakeSession();
        var fakeRecord   = new ModelRecord("model-t", "/fake.onnx", fakeSession, DateTimeOffset.UtcNow, "v1");

        var registryMock = new Mock<IModelRegistry>();
        registryMock.Setup(r => r.GetAsync("model-t", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(fakeRecord);

        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock    = new Mock<IDatabase>();

        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                 .Returns(dbMock.Object);
        dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
              .ReturnsAsync(new RedisValue(json));

        var engine = new InferenceEngine(
            registryMock.Object, redisMock.Object, DefaultOptions(),
            NullLogger<InferenceEngine>.Instance);

        var request = MakeRequest();
        var result  = await engine.RunAsync(request);

        fakeSession.Dispose();

        result.Cached.Should().BeTrue();
        result.RequestId.Should().Be(request.RequestId);   // request ID is stamped on cached result
        result.Outputs.Should().BeEquivalentTo(new[] { 0.9f });
    }

    // ── Redis unavailable ─────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ProceedsWithoutCache_WhenRedisIsNull()
    {
        var registryMock = new Mock<IModelRegistry>();
        registryMock.Setup(r => r.GetAsync("model-t", It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ModelRecord?)null);

        // null IConnectionMultiplexer = no Redis
        var engine = new InferenceEngine(
            registryMock.Object, null, DefaultOptions(), NullLogger<InferenceEngine>.Instance);

        var act = () => engine.RunAsync(MakeRequest()).AsTask();
        // Must throw InvalidOperationException (model not loaded), NOT NullReferenceException
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RunAsync_ProceedsWithoutCache_WhenRedisThrows()
    {
        var registryMock = new Mock<IModelRegistry>();
        registryMock.Setup(r => r.GetAsync("model-t", It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ModelRecord?)null);

        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock    = new Mock<IDatabase>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                 .Returns(dbMock.Object);
        dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
              .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var engine = new InferenceEngine(
            registryMock.Object, redisMock.Object, DefaultOptions(),
            NullLogger<InferenceEngine>.Instance);

        // Redis GET throws → should degrade gracefully and try model lookup
        var act = () => engine.RunAsync(MakeRequest()).AsTask();
        await act.Should().ThrowAsync<InvalidOperationException>("model not loaded");
    }

    // ── Correct model key in result ──────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ThrowsWithCorrectModelKey_InErrorMessage()
    {
        var registryMock = new Mock<IModelRegistry>();
        registryMock.Setup(r => r.GetAsync("model-d", It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ModelRecord?)null);

        var engine = new InferenceEngine(
            registryMock.Object, null, DefaultOptions(), NullLogger<InferenceEngine>.Instance);

        var act = () => engine.RunAsync(MakeRequest("model-d")).AsTask();
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*model-d*");
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ThrowsOperationCanceledException_WhenCancelledBeforeModelLookup()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock    = new Mock<IDatabase>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                 .Returns(dbMock.Object);
        dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
              .ReturnsAsync(RedisValue.Null);

        var registryMock = new Mock<IModelRegistry>();
        registryMock.Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(ValueTask.FromCanceled<ModelRecord?>(cts.Token));

        var engine = new InferenceEngine(
            registryMock.Object, redisMock.Object, DefaultOptions(),
            NullLogger<InferenceEngine>.Instance);

        var act = () => engine.RunAsync(MakeRequest(), cts.Token).AsTask();
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a real <see cref="InferenceSession"/> backed by a minimal valid ONNX model
    /// (Identity op, float32 input/output [1,1]) for use in tests that need a live session.
    /// </summary>
    private static InferenceSession CreateFakeSession()
    {
        byte[] minimalOnnx =
        [
            0x08, 0x07, 0x3a, 0x52, 0x0a, 0x19, 0x0a, 0x05, 0x69, 0x6e, 0x70, 0x75, 0x74,
            0x12, 0x06, 0x6f, 0x75, 0x74, 0x70, 0x75, 0x74, 0x22, 0x08, 0x49, 0x64, 0x65,
            0x6e, 0x74, 0x69, 0x74, 0x79, 0x5a, 0x19, 0x0a, 0x05, 0x69, 0x6e, 0x70, 0x75,
            0x74, 0x12, 0x10, 0x0a, 0x0e, 0x08, 0x01, 0x12, 0x0a, 0x0a, 0x08, 0x0a, 0x02,
            0x08, 0x01, 0x0a, 0x02, 0x08, 0x01, 0x62, 0x1a, 0x0a, 0x06, 0x6f, 0x75, 0x74,
            0x70, 0x75, 0x74, 0x12, 0x10, 0x0a, 0x0e, 0x08, 0x01, 0x12, 0x0a, 0x0a, 0x08,
            0x0a, 0x02, 0x08, 0x01, 0x0a, 0x02, 0x08, 0x01, 0x42, 0x04, 0x0a, 0x00, 0x10, 0x0b,
        ];

        return new InferenceSession(minimalOnnx);
    }
}
