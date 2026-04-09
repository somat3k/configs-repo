namespace MLS.Benchmarks;

/// <summary>
/// Provides a pre-encoded minimal ONNX Identity model for use in benchmark tests
/// that need an ONNX session without a filesystem artifact.
/// <para>
/// The model accepts a float32 tensor of shape [1, 8] and returns the same tensor
/// (Identity operation). This is sufficient to benchmark the ONNX Runtime session
/// path (buffer management, execution provider dispatch, output copy) without a
/// real model-t artifact.
/// </para>
/// <para>
/// Replace with the real model-t ONNX binary (loaded from IPFS / <c>ml-runtime</c>)
/// to obtain accurate production latency numbers.
/// </para>
/// </summary>
internal static class MinimalIdentityModel
{
    // Hand-encoded ONNX protobuf for an Identity model: float32[1,8] → float32[1,8]
    //
    // ModelProto {
    //   ir_version: 7
    //   opset_import { domain: "", version: 17 }
    //   graph {
    //     node { input: "input", output: "output", op_type: "Identity" }
    //     name: "g"
    //     input  { name: "input",  type: { tensor_type { elem_type: 1, shape { dim[1] dim[8] } } } }
    //     output { name: "output", type: { tensor_type { elem_type: 1, shape { dim[1] dim[8] } } } }
    //   }
    // }
    //
    // protobuf field encoding: (field_number << 3) | wire_type
    //   wire_type 0 = varint, 2 = length-delimited

    /// <summary>
    /// Raw ONNX model bytes.  Returned as <see cref="byte[]"/> so callers can pass it
    /// directly to <see cref="Microsoft.ML.OnnxRuntime.InferenceSession"/> without an
    /// extra copy (the constructor accepts <c>byte[]</c>).
    /// </summary>
    public static byte[] ModelBytes => _model;

    private static readonly byte[] _model =
    [
        // ir_version: 7  (field 1, varint)
        0x08, 0x07,

        // opset_import { domain:"", version:17 }  (field 8, length-delimited, 4 bytes)
        0x42, 0x04,
            0x0A, 0x00,        // domain: "" (field 1, len 0)
            0x10, 0x11,        // version: 17 (field 2, varint)

        // graph (field 7, length-delimited, 81 bytes)
        0x3A, 0x51,

            // node (field 1, length-delimited, 25 bytes)
            0x0A, 0x19,
                0x0A, 0x05, 0x69, 0x6E, 0x70, 0x75, 0x74,               // input: "input"
                0x12, 0x06, 0x6F, 0x75, 0x74, 0x70, 0x75, 0x74,         // output: "output"
                0x22, 0x08, 0x49, 0x64, 0x65, 0x6E, 0x74, 0x69, 0x74, 0x79, // op_type: "Identity"

            // name: "g"  (field 2, length-delimited, 1 byte)
            0x12, 0x01, 0x67,

            // input ValueInfoProto (field 11, length-delimited, 23 bytes)
            0x5A, 0x17,
                0x0A, 0x05, 0x69, 0x6E, 0x70, 0x75, 0x74,               // name: "input"
                0x12, 0x0E,                                               // type (14 bytes)
                    0x0A, 0x0C,                                           // tensor_type (12 bytes)
                        0x08, 0x01,                                       // elem_type: FLOAT (1)
                        0x12, 0x08,                                       // shape (8 bytes)
                            0x0A, 0x02, 0x08, 0x01,                      // dim { dim_value: 1 }
                            0x0A, 0x02, 0x08, 0x08,                      // dim { dim_value: 8 }

            // output ValueInfoProto (field 12, length-delimited, 24 bytes)
            0x62, 0x18,
                0x0A, 0x06, 0x6F, 0x75, 0x74, 0x70, 0x75, 0x74,         // name: "output"
                0x12, 0x0E,                                               // type (14 bytes)
                    0x0A, 0x0C,                                           // tensor_type (12 bytes)
                        0x08, 0x01,                                       // elem_type: FLOAT (1)
                        0x12, 0x08,                                       // shape (8 bytes)
                            0x0A, 0x02, 0x08, 0x01,                      // dim { dim_value: 1 }
                            0x0A, 0x02, 0x08, 0x08,                      // dim { dim_value: 8 }
    ];
}
