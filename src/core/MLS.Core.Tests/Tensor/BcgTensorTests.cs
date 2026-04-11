using System.Text.Json;
using FluentAssertions;
using MLS.Core.Tensor;
using Xunit;

namespace MLS.Core.Tests.Tensor;

/// <summary>
/// Unit tests for <see cref="BcgTensor"/> creation, derivation, and invariants.
/// </summary>
public sealed class BcgTensorTests
{
    private static readonly JsonElement EmptyObject =
        JsonDocument.Parse("{}").RootElement;

    private static readonly JsonElement FloatArray =
        JsonDocument.Parse("[1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0]").RootElement;

    // ── CreateRoot ────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateRoot_AssignsNewId()
    {
        var t1 = MakeRoot();
        var t2 = MakeRoot();
        t1.Id.Should().NotBe(t2.Id);
    }

    [Fact]
    public void CreateRoot_IsRootAndHasEmptyLineage()
    {
        var tensor = MakeRoot();
        tensor.IsRoot.Should().BeTrue();
        tensor.Lineage.Should().BeEmpty();
    }

    [Fact]
    public void CreateRoot_TransportClassIsInline()
    {
        var tensor = MakeRoot();
        tensor.TransportClass.Should().Be(TensorTransportClass.Inline);
    }

    [Fact]
    public void CreateRoot_MetaContractVersionMatchesCurrent()
    {
        var tensor = MakeRoot();
        tensor.Meta.ContractVersion.Should().Be(TensorMeta.CurrentContractVersion);
    }

    [Fact]
    public void CreateRoot_NoPersistenceOrIntegrity()
    {
        var tensor = MakeRoot();
        tensor.Persistence.Should().BeNull();
        tensor.Integrity.Should().BeNull();
    }

    [Fact]
    public void CreateRoot_TraceIdPreserved()
    {
        var traceId = Guid.NewGuid();
        var tensor = BcgTensor.CreateRoot(
            dtype: TensorDType.Float32,
            shape: [1, 7],
            layout: TensorLayout.Dense,
            shapeClass: TensorShapeClass.ExactStatic,
            data: FloatArray,
            encoding: TensorEncoding.RawFloat32LE,
            originModuleId: "trader",
            traceId: traceId);

        tensor.Meta.TraceId.Should().Be(traceId);
    }

    [Fact]
    public void CreateRoot_OriginModuleIdPreserved()
    {
        var tensor = BcgTensor.CreateRoot(
            dtype: TensorDType.Float32,
            shape: [1, 7],
            layout: TensorLayout.Dense,
            shapeClass: TensorShapeClass.ExactStatic,
            data: FloatArray,
            encoding: TensorEncoding.RawFloat32LE,
            originModuleId: "ml-runtime",
            traceId: Guid.NewGuid());

        tensor.Meta.OriginModuleId.Should().Be("ml-runtime");
    }

    // ── ElementCount ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(new[] { 1, 7 }, 7L)]
    [InlineData(new[] { 32, 128 }, 4096L)]
    [InlineData(new int[] { }, 1L)]  // scalar
    public void ElementCount_ReturnsCorrectProduct(int[] shape, long expected)
    {
        var tensor = BcgTensor.CreateRoot(
            dtype: TensorDType.Float32,
            shape: shape,
            layout: TensorLayout.Dense,
            shapeClass: TensorShapeClass.ExactStatic,
            data: EmptyObject,
            encoding: TensorEncoding.RawFloat32LE,
            originModuleId: "test",
            traceId: Guid.NewGuid());

        tensor.ElementCount.Should().Be(expected);
    }

    [Fact]
    public void ElementCount_DynamicDimension_ReturnsNegativeOne()
    {
        var tensor = BcgTensor.CreateRoot(
            dtype: TensorDType.Float32,
            shape: [-1, 128],
            layout: TensorLayout.Dense,
            shapeClass: TensorShapeClass.BoundedDynamic,
            data: EmptyObject,
            encoding: TensorEncoding.RawFloat32LE,
            originModuleId: "test",
            traceId: Guid.NewGuid());

        tensor.ElementCount.Should().Be(-1L);
    }

    // ── Derive ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Derive_ProducesNewId()
    {
        var parent = MakeRoot();
        var derived = parent.Derive(
            dtype: TensorDType.Float32,
            shape: [7],
            layout: TensorLayout.Dense,
            shapeClass: TensorShapeClass.ExactStatic,
            data: FloatArray,
            encoding: TensorEncoding.RawFloat32LE,
            lineageStep: MakeLineageStep(parent.Id));

        derived.Id.Should().NotBe(parent.Id);
    }

    [Fact]
    public void Derive_InheritsTraceId()
    {
        var parent = MakeRoot();
        var derived = parent.Derive(
            dtype: TensorDType.Float32,
            shape: [7],
            layout: TensorLayout.Dense,
            shapeClass: TensorShapeClass.ExactStatic,
            data: FloatArray,
            encoding: TensorEncoding.RawFloat32LE,
            lineageStep: MakeLineageStep(parent.Id));

        derived.Meta.TraceId.Should().Be(parent.Meta.TraceId);
    }

    [Fact]
    public void Derive_IsNotRoot_AndLineageHasOneEntry()
    {
        var parent = MakeRoot();
        var derived = parent.Derive(
            dtype: TensorDType.Float32,
            shape: [7],
            layout: TensorLayout.Dense,
            shapeClass: TensorShapeClass.ExactStatic,
            data: FloatArray,
            encoding: TensorEncoding.RawFloat32LE,
            lineageStep: MakeLineageStep(parent.Id));

        derived.IsRoot.Should().BeFalse();
        derived.Lineage.Should().HaveCount(1);
        derived.Lineage[0].ParentTensorIds.Should().Contain(parent.Id);
    }

    [Fact]
    public void Derive_MultipleGenerations_AccumulatesLineage()
    {
        var root = MakeRoot();
        var gen1 = root.Derive(TensorDType.Float32, [7], TensorLayout.Dense,
            TensorShapeClass.ExactStatic, FloatArray, TensorEncoding.RawFloat32LE,
            MakeLineageStep(root.Id));
        var gen2 = gen1.Derive(TensorDType.Float32, [7], TensorLayout.Dense,
            TensorShapeClass.ExactStatic, FloatArray, TensorEncoding.RawFloat32LE,
            MakeLineageStep(gen1.Id));

        gen2.Lineage.Should().HaveCount(2);
    }

    // ── TensorLineageRecord.Create ────────────────────────────────────────────────

    [Fact]
    public void LineageRecord_Create_AssignsNewId()
    {
        var l1 = TensorLineageRecord.Create([Guid.NewGuid()], "reshape", "ml-runtime", "1.0", ["reshape:[1,7]→[7]"]);
        var l2 = TensorLineageRecord.Create([Guid.NewGuid()], "reshape", "ml-runtime", "1.0", ["reshape:[1,7]→[7]"]);
        l1.LineageId.Should().NotBe(l2.LineageId);
    }

    [Fact]
    public void LineageRecord_Create_LossyCastFlagDefaultsFalse()
    {
        var step = TensorLineageRecord.Create([Guid.NewGuid()], "cast", "test", "1.0", ["cast:float64→float32"]);
        step.IsLossyCast.Should().BeFalse();
    }

    [Fact]
    public void LineageRecord_Create_LossyCastFlagPreserved()
    {
        var step = TensorLineageRecord.Create(
            parentTensorIds: [Guid.NewGuid()],
            transformationStepId: "lossy-cast",
            producingModuleId: "transformation-bus",
            kernelVersion: "1.0",
            operations: ["cast:float64→float32"],
            isLossyCast: true);

        step.IsLossyCast.Should().BeTrue();
    }

    // ── TensorPersistenceRef ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(TensorStorageMode.Redis, true)]
    [InlineData(TensorStorageMode.Postgres, true)]
    [InlineData(TensorStorageMode.Ipfs, true)]
    [InlineData(TensorStorageMode.Transient, false)]
    public void PersistenceRef_IsExternalized_CorrectForStorageMode(TensorStorageMode mode, bool expected)
    {
        var pref = new TensorPersistenceRef(
            RedisKey: null,
            PostgresRecordId: null,
            IpfsCid: null,
            StorageMode: mode,
            ExpiresAt: null);

        pref.IsExternalized.Should().Be(expected);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static BcgTensor MakeRoot() =>
        BcgTensor.CreateRoot(
            dtype: TensorDType.Float32,
            shape: [1, 7],
            layout: TensorLayout.Dense,
            shapeClass: TensorShapeClass.ExactStatic,
            data: FloatArray,
            encoding: TensorEncoding.RawFloat32LE,
            originModuleId: "trader",
            traceId: Guid.NewGuid());

    private static TensorLineageRecord MakeLineageStep(Guid parentId) =>
        TensorLineageRecord.Create(
            parentTensorIds: [parentId],
            transformationStepId: "reshape",
            producingModuleId: "transformation-bus",
            kernelVersion: "1.0",
            operations: ["reshape:[1,7]→[7]"]);
}
