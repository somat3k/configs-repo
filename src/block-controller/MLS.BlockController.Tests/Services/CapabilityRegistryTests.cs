using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MLS.BlockController.Models;
using MLS.BlockController.Services;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using Moq;
using Xunit;

namespace MLS.BlockController.Tests.Services;

/// <summary>
/// Verifies <see cref="InMemoryCapabilityRegistry"/> stores, resolves, updates, and evicts records.
/// QA gates: BC-REG-01 through BC-REG-06.
/// </summary>
public sealed class CapabilityRegistryTests
{
    private readonly Mock<IMessageRouter> _routerMock = new();
    private readonly InMemoryCapabilityRegistry _registry;

    public CapabilityRegistryTests()
    {
        _routerMock
            .Setup(r => r.BroadcastAsync(It.IsAny<EnvelopePayload>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _registry = new InMemoryCapabilityRegistry(
            _routerMock.Object,
            NullLogger<InMemoryCapabilityRegistry>.Instance);
    }

    // ── BC-REG-02 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_StoresCapabilityRecord_RetrievableByModuleId()
    {
        var moduleId = Guid.NewGuid();
        var record = MakeRecord(moduleId, ["INFERENCE_REQUEST"]);

        await _registry.RegisterAsync(record);

        var stored = await _registry.GetAsync(moduleId);
        stored.Should().NotBeNull();
        stored!.ModuleId.Should().Be(moduleId);
        stored.OperationTypes.Should().Contain("INFERENCE_REQUEST");
    }

    // ── BC-REG-03 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveByOperation_ReturnsAllCapableModules()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var idOther = Guid.NewGuid();

        await _registry.RegisterAsync(MakeRecord(id1, ["INFERENCE_REQUEST"]));
        await _registry.RegisterAsync(MakeRecord(id2, ["INFERENCE_REQUEST", "TRADE_SIGNAL"]));
        await _registry.RegisterAsync(MakeRecord(idOther, ["TRADE_SIGNAL"]));

        var results = await _registry.ResolveByOperationAsync("INFERENCE_REQUEST");

        results.Should().HaveCount(2);
        results.Select(r => r.ModuleId).Should().Contain([id1, id2]);
        results.Select(r => r.ModuleId).Should().NotContain(idOther);
    }

    [Fact]
    public async Task ResolveByOperation_EmptyRegistry_ReturnsEmptyList()
    {
        var results = await _registry.ResolveByOperationAsync("INFERENCE_REQUEST");
        results.Should().BeEmpty();
    }

    // ── BC-REG-04 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_BroadcastsModuleCapabilityUpdated()
    {
        var record = MakeRecord(Guid.NewGuid(), ["TRADE_SIGNAL"]);
        await _registry.RegisterAsync(record);

        _routerMock.Verify(
            r => r.BroadcastAsync(
                It.Is<EnvelopePayload>(e => e.Type == MessageTypes.ModuleCapabilityUpdated),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── BC-REG-05 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Evict_RemovesRecordFromAllIndexes()
    {
        var moduleId = Guid.NewGuid();
        await _registry.RegisterAsync(MakeRecord(moduleId, ["INFERENCE_REQUEST"]));

        await _registry.EvictAsync(moduleId);

        var byId = await _registry.GetAsync(moduleId);
        var byOp = await _registry.ResolveByOperationAsync("INFERENCE_REQUEST");

        byId.Should().BeNull();
        byOp.Should().NotContain(r => r.ModuleId == moduleId);
    }

    [Fact]
    public async Task Evict_NonexistentModule_IsNoOp()
    {
        var act = async () => await _registry.EvictAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    // ── BC-REG-06 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_DuplicateModuleId_ReplacesRecord()
    {
        var moduleId = Guid.NewGuid();
        await _registry.RegisterAsync(MakeRecord(moduleId, ["INFERENCE_REQUEST"]));
        await _registry.RegisterAsync(MakeRecord(moduleId, ["TRADE_SIGNAL"]));

        var stored = await _registry.GetAsync(moduleId);
        stored!.OperationTypes.Should().Contain("TRADE_SIGNAL");
        stored.OperationTypes.Should().NotContain("INFERENCE_REQUEST");
    }

    // ── Specialist ordering ───────────────────────────────────────────────────

    [Fact]
    public async Task ResolveByOperation_SpecialistModuleRanksHigher()
    {
        var specialist = Guid.NewGuid();
        var generalist = Guid.NewGuid();

        // Specialist: only INFERENCE_REQUEST → higher match score
        await _registry.RegisterAsync(MakeRecord(specialist, ["INFERENCE_REQUEST"]));
        // Generalist: many operations → lower match score
        await _registry.RegisterAsync(MakeRecord(generalist,
            ["INFERENCE_REQUEST", "TRADE_SIGNAL", "ORDER_CREATE", "SHELL_OUTPUT", "SHELL_INPUT"]));

        var results = await _registry.ResolveByOperationAsync("INFERENCE_REQUEST");

        results.Should().HaveCount(2);
        results[0].ModuleId.Should().Be(specialist);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static CapabilityRecord MakeRecord(Guid moduleId, IReadOnlyList<string> ops) =>
        new(ModuleId: moduleId,
            ModuleName: $"test-module-{moduleId:N}",
            OperationTypes: ops,
            TensorClassesIn: [],
            TensorClassesOut: [],
            TransportInterfaces: ["websocket"],
            BatchSupport: "none",
            StreamingSupport: "none",
            IsStateful: false,
            Version: "1.0.0",
            RegisteredAt: DateTimeOffset.UtcNow,
            LastUpdatedAt: DateTimeOffset.UtcNow);
}
