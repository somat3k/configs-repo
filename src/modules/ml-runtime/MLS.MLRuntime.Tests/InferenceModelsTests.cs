using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MLS.MLRuntime.Configuration;
using MLS.MLRuntime.Inference;
using Xunit;

namespace MLS.MLRuntime.Tests;

/// <summary>
/// Tests for <see cref="InferenceRequestPayload"/>, <see cref="InferenceResultPayload"/>,
/// and <see cref="MLRuntimeOptions"/> defaults.
/// </summary>
public sealed class InferenceModelsTests
{
    // ── InferenceRequestPayload ───────────────────────────────────────────────

    [Fact]
    public void InferenceRequestPayload_SerializesWithCorrectPropertyNames()
    {
        var request = new InferenceRequestPayload(
            RequestId:         Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ModelKey:          "model-t",
            Features:          [1f, 2f, 3f],
            RequesterModuleId: "trader");

        var json = JsonSerializer.Serialize(request);

        json.Should().Contain("\"request_id\"");
        json.Should().Contain("\"model_key\"");
        json.Should().Contain("\"features\"");
        json.Should().Contain("\"requester_module_id\"");
        json.Should().Contain("\"model-t\"");
        json.Should().Contain("\"trader\"");
    }

    [Fact]
    public void InferenceRequestPayload_RoundTrips_ThroughJson()
    {
        var original = new InferenceRequestPayload(
            RequestId:         Guid.NewGuid(),
            ModelKey:          "model-a",
            Features:          [0.5f, -1.2f, 3.14f],
            RequesterModuleId: "arbitrager");

        var json     = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<InferenceRequestPayload>(json);

        restored.Should().NotBeNull();
        restored!.RequestId.Should().Be(original.RequestId);
        restored.ModelKey.Should().Be(original.ModelKey);
        restored.Features.Should().BeEquivalentTo(original.Features);
        restored.RequesterModuleId.Should().Be(original.RequesterModuleId);
    }

    [Fact]
    public void InferenceRequestPayload_AcceptsEmptyFeatureArray()
    {
        var request = new InferenceRequestPayload(Guid.NewGuid(), "model-d", [], "defi");
        var json    = JsonSerializer.Serialize(request);
        var restored = JsonSerializer.Deserialize<InferenceRequestPayload>(json);

        restored!.Features.Should().BeEmpty();
    }

    // ── InferenceResultPayload ────────────────────────────────────────────────

    [Fact]
    public void InferenceResultPayload_SerializesWithCorrectPropertyNames()
    {
        var result = new InferenceResultPayload(
            RequestId: Guid.NewGuid(),
            ModelKey:  "model-t",
            Outputs:   [0.8f, 0.2f],
            LatencyMs: 4.5,
            Cached:    false,
            ModelId:   "model-t-v1");

        var json = JsonSerializer.Serialize(result);

        json.Should().Contain("\"request_id\"");
        json.Should().Contain("\"model_key\"");
        json.Should().Contain("\"outputs\"");
        json.Should().Contain("\"latency_ms\"");
        json.Should().Contain("\"cached\"");
        json.Should().Contain("\"model_id\"");
    }

    [Fact]
    public void InferenceResultPayload_RoundTrips_ThroughJson()
    {
        var original = new InferenceResultPayload(
            RequestId: Guid.NewGuid(),
            ModelKey:  "model-a",
            Outputs:   [0.95f, 0.05f],
            LatencyMs: 7.3,
            Cached:    true,
            ModelId:   "model-a-v2");

        var json     = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<InferenceResultPayload>(json);

        restored.Should().NotBeNull();
        restored!.RequestId.Should().Be(original.RequestId);
        restored.ModelKey.Should().Be(original.ModelKey);
        restored.Outputs.Should().BeEquivalentTo(original.Outputs);
        restored.LatencyMs.Should().BeApproximately(original.LatencyMs, 0.001);
        restored.Cached.Should().Be(original.Cached);
        restored.ModelId.Should().Be(original.ModelId);
    }

    [Fact]
    public void InferenceResultPayload_Accepts_NullModelId()
    {
        var result   = new InferenceResultPayload(Guid.NewGuid(), "model-d", [], 1.0, false, null);
        var json     = JsonSerializer.Serialize(result);
        var restored = JsonSerializer.Deserialize<InferenceResultPayload>(json);

        restored!.ModelId.Should().BeNull();
    }

    // ── MLRuntimeOptions defaults ─────────────────────────────────────────────

    [Fact]
    public void MLRuntimeOptions_HasCorrectDefaultPorts()
    {
        var opts = new MLRuntimeOptions();
        opts.HttpEndpoint.Should().Contain("5600");
        opts.WsEndpoint.Should().Contain("6600");
    }

    [Fact]
    public void MLRuntimeOptions_HasCorrectDefaultBlockControllerUrl()
    {
        var opts = new MLRuntimeOptions();
        opts.BlockControllerUrl.Should().Contain("block-controller");
        opts.BlockControllerUrl.Should().Contain("5100");
    }

    [Fact]
    public void MLRuntimeOptions_DefaultRedisTtl_IsPositive()
    {
        var opts = new MLRuntimeOptions();
        opts.RedisCacheTtlSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MLRuntimeOptions_DefaultInferenceTimeout_IsPositive()
    {
        var opts = new MLRuntimeOptions();
        opts.InferenceTimeoutMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MLRuntimeOptions_BindsFromIOptions()
    {
        var opts = Options.Create(new MLRuntimeOptions
        {
            InferenceTimeoutMs   = 100,
            RedisCacheTtlSeconds = 60,
            ModelBasePath        = "/custom/models",
        });

        opts.Value.InferenceTimeoutMs.Should().Be(100);
        opts.Value.RedisCacheTtlSeconds.Should().Be(60);
        opts.Value.ModelBasePath.Should().Be("/custom/models");
    }
}
