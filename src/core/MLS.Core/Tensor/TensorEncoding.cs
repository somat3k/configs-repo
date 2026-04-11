namespace MLS.Core.Tensor;

/// <summary>
/// Serialisation encoding of the inline <see cref="BcgTensor.Data"/> payload.
/// The encoding describes the binary or text format of the data bytes — not the semantic dtype.
/// </summary>
public enum TensorEncoding
{
    /// <summary>
    /// Raw little-endian IEEE 754 float32 bytes (row-major).
    /// Default for dense numeric tensors flowing through ONNX inference paths.
    /// </summary>
    RawFloat32LE,
    /// <summary>Raw little-endian IEEE 754 float64 bytes.</summary>
    RawFloat64LE,
    /// <summary>Raw little-endian int32 bytes.</summary>
    RawInt32LE,
    /// <summary>Raw little-endian int64 bytes.</summary>
    RawInt64LE,
    /// <summary>Raw uint8 bytes.</summary>
    RawUInt8,
    /// <summary>Raw boolean bytes (1 byte per element, 0x00 = false, 0x01 = true).</summary>
    RawBool,

    /// <summary>
    /// UTF-8 encoded JSON object or array.
    /// Used for <see cref="TensorDType.JsonSemantic"/> and small structured payloads.
    /// </summary>
    JsonUtf8,

    /// <summary>
    /// MessagePack binary encoding.
    /// Preferred for high-throughput wire transport of structured payloads.
    /// </summary>
    MessagePack,

    /// <summary>
    /// Raw UTF-8 string bytes with no additional framing.
    /// Used for <see cref="TensorDType.String"/> and <see cref="TensorDType.CodeTokenStream"/>.
    /// </summary>
    Utf8String,

    /// <summary>
    /// Uninterpreted binary blob. Semantic interpretation must be provided by the semantic tag.
    /// </summary>
    OpaqueBytes,
}
