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

    [Fact]
    public void PersistenceRef_Redis_IsExternalized_True()
    {
        var pref = new TensorPersistenceRef(
            RedisKey: "tensor:abc123",
            PostgresRecordId: null,
            IpfsCid: null,
            StorageMode: TensorStorageMode.Redis,
            ExpiresAt: null);

        pref.IsExternalized.Should().BeTrue();
    }

    [Fact]
    public void PersistenceRef_Postgres_IsExternalized_True()
    {
        var pref = new TensorPersistenceRef(
            RedisKey: null,
            PostgresRecordId: Guid.NewGuid(),
            IpfsCid: null,
            StorageMode: TensorStorageMode.Postgres,
            ExpiresAt: null);

        pref.IsExternalized.Should().BeTrue();
    }

    [Fact]
    public void PersistenceRef_Ipfs_IsExternalized_True()
    {
        var pref = new TensorPersistenceRef(
            RedisKey: null,
            PostgresRecordId: null,
            IpfsCid: "bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi",
            StorageMode: TensorStorageMode.Ipfs,
            ExpiresAt: null);

        pref.IsExternalized.Should().BeTrue();
    }

    [Fact]
    public void PersistenceRef_Transient_IsExternalized_False()
    {
        var pref = new TensorPersistenceRef(
            RedisKey: null,
            PostgresRecordId: null,
            IpfsCid: null,
            StorageMode: TensorStorageMode.Transient,
            ExpiresAt: null);

        pref.IsExternalized.Should().BeFalse();
    }

    [Fact]
    public void PersistenceRef_Redis_MissingKey_ThrowsArgumentException()
    {
        var act = () => new TensorPersistenceRef(
            RedisKey: null,
            PostgresRecordId: null,
            IpfsCid: null,
            StorageMode: TensorStorageMode.Redis,
            ExpiresAt: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PersistenceRef_Postgres_MissingId_ThrowsArgumentException()
    {
        var act = () => new TensorPersistenceRef(
            RedisKey: null,
            PostgresRecordId: null,
            IpfsCid: null,
            StorageMode: TensorStorageMode.Postgres,
            ExpiresAt: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PersistenceRef_Ipfs_MissingCid_ThrowsArgumentException()
    {
        var act = () => new TensorPersistenceRef(
            RedisKey: null,
            PostgresRecordId: null,
            IpfsCid: null,
            StorageMode: TensorStorageMode.Ipfs,
            ExpiresAt: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PersistenceRef_Transient_WithRedisKey_ThrowsArgumentException()
    {
        var act = () => new TensorPersistenceRef(
            RedisKey: "tensor:abc",
            PostgresRecordId: null,
            IpfsCid: null,
            StorageMode: TensorStorageMode.Transient,
            ExpiresAt: null);

        act.Should().Throw<ArgumentException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    // ── CreateRoot validation ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRoot_EmptyOriginModuleId_Throws(string moduleId)
    {
        var act = () => BcgTensor.CreateRoot(
            dtype: TensorDType.Float32,
            shape: [1, 7],
            layout: TensorLayout.Dense,
            shapeClass: TensorShapeClass.ExactStatic,
            data: FloatArray,
            encoding: TensorEncoding.RawFloat32LE,
            originModuleId: moduleId,
            traceId: Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateRoot_DataIsCloned_SafeAfterDocumentParsed()
    {
        // Arrange: parse into a local document (simulates a caller that may GC/dispose it)
        var doc = JsonDocument.Parse("[1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0]");
        var element = doc.RootElement;

        // Act: create tensor; CreateRoot must clone the element
        var tensor = BcgTensor.CreateRoot(
            dtype: TensorDType.Float32,
            shape: [1, 7],
            layout: TensorLayout.Dense,
            shapeClass: TensorShapeClass.ExactStatic,
            data: element,
            encoding: TensorEncoding.RawFloat32LE,
            originModuleId: "ml-runtime",
            traceId: Guid.NewGuid());

        // Dispose the original document
        doc.Dispose();

        // Assert: the cloned JsonElement is still readable
        var act = () => tensor.Data!.Value.GetArrayLength();
        act.Should().NotThrow();
        tensor.Data!.Value.GetArrayLength().Should().Be(7);
    }

    // ── CreateReference factory ───────────────────────────────────────────────────

    [Fact]
    public void CreateReference_TransportClassIsReference_DataIsNull()
    {
        var persistence = new TensorPersistenceRef(
            RedisKey: null,
            PostgresRecordId: null,
            IpfsCid: "bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi",
            StorageMode: TensorStorageMode.Ipfs,
            ExpiresAt: null);

        var tensor = BcgTensor.CreateReference(
            dtype: TensorDType.Float32,
            shape: [1, 7],
            layout: TensorLayout.Dense,
            shapeClass: TensorShapeClass.ExactStatic,
            encoding: TensorEncoding.RawFloat32LE,
            originModuleId: "ml-runtime",
            traceId: Guid.NewGuid(),
            persistence: persistence);

        tensor.TransportClass.Should().Be(TensorTransportClass.Reference);
        tensor.Data.Should().BeNull();
        tensor.Persistence.Should().Be(persistence);
        tensor.IsRoot.Should().BeTrue();
    }

    [Fact]
    public void CreateReference_EmptyOriginModuleId_Throws()
    {
        var persistence = new TensorPersistenceRef(
            RedisKey: null,
            PostgresRecordId: null,
            IpfsCid: "bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi",
            StorageMode: TensorStorageMode.Ipfs,
            ExpiresAt: null);

        var act = () => BcgTensor.CreateReference(
            dtype: TensorDType.Float32,
            shape: [1, 7],
            layout: TensorLayout.Dense,
            shapeClass: TensorShapeClass.ExactStatic,
            encoding: TensorEncoding.RawFloat32LE,
            originModuleId: " ",
            traceId: Guid.NewGuid(),
            persistence: persistence);

        act.Should().Throw<ArgumentException>();
    }

    // ── DeriveReference factory ───────────────────────────────────────────────────

    [Fact]
    public void DeriveReference_TransportClassIsReference_DataIsNull_LineageExtended()
    {
        var parent = MakeRoot();
        var persistence = new TensorPersistenceRef(
            RedisKey: null,
            PostgresRecordId: null,
            IpfsCid: "bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi",
            StorageMode: TensorStorageMode.Ipfs,
            ExpiresAt: null);

        var derived = parent.DeriveReference(
            dtype: TensorDType.Float32,
            shape: [1, 7],
            layout: TensorLayout.Dense,
            shapeClass: TensorShapeClass.ExactStatic,
            encoding: TensorEncoding.RawFloat32LE,
            lineageStep: MakeLineageStep(parent.Id),
            persistence: persistence);

        derived.TransportClass.Should().Be(TensorTransportClass.Reference);
        derived.Data.Should().BeNull();
        derived.Persistence.Should().Be(persistence);
        derived.IsRoot.Should().BeFalse();
        derived.Lineage.Should().HaveCount(1);
        derived.Meta.TraceId.Should().Be(parent.Meta.TraceId);
    }

    // ── TensorLineageRecord validation ────────────────────────────────────────────

    [Fact]
    public void LineageRecord_Create_EmptyParentIds_Throws()
    {
        var act = () => TensorLineageRecord.Create([], "reshape", "bus", "1.0", ["reshape"]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LineageRecord_Create_EmptyGuidInParentIds_Throws()
    {
        var act = () => TensorLineageRecord.Create(
            [Guid.Empty], "reshape", "bus", "1.0", ["reshape"]);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void LineageRecord_Create_EmptyTransformationStepId_Throws(string stepId)
    {
        var act = () => TensorLineageRecord.Create(
            [Guid.NewGuid()], stepId, "bus", "1.0", ["reshape"]);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void LineageRecord_Create_EmptyProducingModuleId_Throws(string moduleId)
    {
        var act = () => TensorLineageRecord.Create(
            [Guid.NewGuid()], "reshape", moduleId, "1.0", ["reshape"]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LineageRecord_Create_MutatingInputList_DoesNotAffectRecord()
    {
        // Verify that Create defensively copies inputs
        var parentIds = new List<Guid> { Guid.NewGuid() };
        var ops = new List<string> { "reshape:[1,7]→[7]" };

        var step = TensorLineageRecord.Create(parentIds, "reshape", "bus", "1.0", ops);

        // Mutate originals
        parentIds.Add(Guid.NewGuid());
        ops.Add("extra-op");

        // Record should be unchanged
        step.ParentTensorIds.Should().HaveCount(1);
        step.Operations.Should().HaveCount(1);
    }

    // ── ElementCount overflow ─────────────────────────────────────────────────────

    [Fact]
    public void ElementCount_VeryLargeShape_ReturnsNegativeOne()
    {
        // Three dimensions of int.MaxValue overflows long (2^31-1)^3 > long.MaxValue
        var tensor = BcgTensor.CreateRoot(
            dtype: TensorDType.Float32,
            shape: [int.MaxValue, int.MaxValue, int.MaxValue],
            layout: TensorLayout.Dense,
            shapeClass: TensorShapeClass.ExactStatic,
            data: EmptyObject,
            encoding: TensorEncoding.RawFloat32LE,
            originModuleId: "test",
            traceId: Guid.NewGuid());

        tensor.ElementCount.Should().Be(-1L);
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
