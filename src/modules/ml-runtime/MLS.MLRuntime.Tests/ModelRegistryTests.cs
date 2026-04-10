using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Moq;
using MLS.MLRuntime.Models;
using Xunit;

namespace MLS.MLRuntime.Tests;

/// <summary>
/// Unit tests for <see cref="ModelRegistry"/>.
/// </summary>
public sealed class ModelRegistryTests : IDisposable
{
    private readonly Mock<IInferenceSessionFactory> _factoryMock = new();
    private readonly ModelRegistry _registry;

    public ModelRegistryTests()
    {
        _registry = new ModelRegistry(
            _factoryMock.Object,
            NullLogger<ModelRegistry>.Instance);
    }

    public void Dispose() => _registry.Dispose();

    // ── GetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenModelNotLoaded()
    {
        var result = await _registry.GetAsync("model-t");
        result.Should().BeNull();
    }

    [Fact]
    public void Loaded_ReturnsEmptyDictionary_Initially()
    {
        _registry.Loaded.Should().BeEmpty();
    }

    // ── LoadAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_ThrowsFileNotFoundException_WhenFileDoesNotExist()
    {
        var act = () => _registry.LoadAsync("model-t", "/nonexistent/model.onnx");
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task LoadAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _registry.LoadAsync("model-t", "/any/path.onnx", ct: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LoadAsync_AddsModelRecord_WhenFileExists()
    {
        var path    = Path.GetTempFileName(); // creates an actual file
        var session = CreateFakeSession();
        _factoryMock.Setup(f => f.Create(path, It.IsAny<SessionOptions>())).Returns(session);

        try
        {
            await _registry.LoadAsync("model-t", path, "v1");

            _registry.Loaded.Should().ContainKey("model-t");
            var record = _registry.Loaded["model-t"];
            record.ModelKey.Should().Be("model-t");
            record.ModelId.Should().Be("v1");
            record.ModelPath.Should().Be(path);
        }
        finally
        {
            File.Delete(path);
            session.Dispose();
        }
    }

    [Fact]
    public async Task GetAsync_ReturnsRecord_AfterLoad()
    {
        var path    = Path.GetTempFileName();
        var session = CreateFakeSession();
        _factoryMock.Setup(f => f.Create(path, It.IsAny<SessionOptions>())).Returns(session);

        try
        {
            await _registry.LoadAsync("model-a", path);
            var record = await _registry.GetAsync("model-a");
            record.Should().NotBeNull();
            record!.ModelKey.Should().Be("model-a");
        }
        finally
        {
            File.Delete(path);
            session.Dispose();
        }
    }

    [Fact]
    public async Task LoadAsync_IsCaseInsensitive_ForModelKey()
    {
        var path    = Path.GetTempFileName();
        var session = CreateFakeSession();
        _factoryMock.Setup(f => f.Create(path, It.IsAny<SessionOptions>())).Returns(session);

        try
        {
            await _registry.LoadAsync("MODEL-T", path);
            var record = await _registry.GetAsync("model-t");
            record.Should().NotBeNull();
        }
        finally
        {
            File.Delete(path);
            session.Dispose();
        }
    }

    // ── UnloadAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UnloadAsync_RemovesModel_WhenLoaded()
    {
        var path    = Path.GetTempFileName();
        var session = CreateFakeSession();
        _factoryMock.Setup(f => f.Create(path, It.IsAny<SessionOptions>())).Returns(session);

        try
        {
            await _registry.LoadAsync("model-d", path);
            await _registry.UnloadAsync("model-d");

            _registry.Loaded.Should().NotContainKey("model-d");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task UnloadAsync_DoesNotThrow_WhenKeyNotFound()
    {
        var act = () => _registry.UnloadAsync("nonexistent-key");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UnloadAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _registry.UnloadAsync("model-t", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Hot-reload ────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_HotReloads_WhenModelKeyAlreadyLoaded()
    {
        var path1    = Path.GetTempFileName();
        var path2    = Path.GetTempFileName();
        var session1 = CreateFakeSession();
        var session2 = CreateFakeSession();

        _factoryMock.Setup(f => f.Create(path1, It.IsAny<SessionOptions>())).Returns(session1);
        _factoryMock.Setup(f => f.Create(path2, It.IsAny<SessionOptions>())).Returns(session2);

        try
        {
            await _registry.LoadAsync("model-t", path1, "v1");
            await _registry.LoadAsync("model-t", path2, "v2");

            var record = await _registry.GetAsync("model-t");
            record.Should().NotBeNull();
            record!.ModelId.Should().Be("v2");
            record.ModelPath.Should().Be(path2);
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
            session2.Dispose();
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a real <see cref="InferenceSession"/> backed by a minimal valid ONNX model
    /// (Identity op, float32 input/output [1,1]) generated from correct protobuf bytes.
    /// </summary>
    private static InferenceSession CreateFakeSession()
    {
        // Minimal ONNX model: Identity op, float32 input "input" / output "output", shape [1,1].
        // ir_version=7, opset=11, encoded as raw protobuf bytes.
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
