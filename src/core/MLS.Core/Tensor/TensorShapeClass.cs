namespace MLS.Core.Tensor;

/// <summary>
/// Shape tolerance class that a module or execution lane declares for tensor acceptance.
/// Shape is a runtime constraint: the Block Controller governor may reject, reroute,
/// or invoke transformation when a tensor's shape class is incompatible with the target lane.
/// </summary>
public enum TensorShapeClass
{
    /// <summary>
    /// All dimensions are known at tensor creation time and must match exactly.
    /// Required for compiled and static-batch-optimised execution lanes.
    /// </summary>
    ExactStatic,

    /// <summary>
    /// One or more dimensions may vary within a declared upper bound per axis.
    /// The module contract must specify the maximum size for each dynamic axis.
    /// </summary>
    BoundedDynamic,

    /// <summary>
    /// Rows (or the primary axis) may have variable lengths.
    /// Accepted only in lanes that explicitly declare ragged support.
    /// </summary>
    RaggedStructured,

    /// <summary>
    /// Non-zero value density is declared but dimensions may be large.
    /// Accepted only in lanes that explicitly declare sparse support.
    /// </summary>
    SparseStructured,

    /// <summary>
    /// Shape is determined by graph topology and may change between executions.
    /// Only accepted in graph-aware execution lanes.
    /// </summary>
    GraphDerivedVariable,
}
