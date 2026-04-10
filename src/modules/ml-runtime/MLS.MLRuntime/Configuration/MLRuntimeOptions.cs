namespace MLS.MLRuntime.Configuration;

/// <summary>
/// Configuration options for the ML Runtime module, bound from <c>appsettings.json</c>
/// under the <c>"MLRuntime"</c> section.
/// </summary>
public sealed class MLRuntimeOptions
{
    /// <summary>HTTP endpoint of this module, e.g. <c>http://ml-runtime:5600</c>.</summary>
    public string HttpEndpoint { get; set; } = "http://ml-runtime:5600";

    /// <summary>WebSocket endpoint of this module, e.g. <c>ws://ml-runtime:6600</c>.</summary>
    public string WsEndpoint { get; set; } = "ws://ml-runtime:6600";

    /// <summary>Block Controller HTTP base URL.</summary>
    public string BlockControllerUrl { get; set; } = "http://block-controller:5100";

    /// <summary>PostgreSQL connection string.</summary>
    public string PostgresConnectionString { get; set; } =
        "Host=postgres;Port=5432;Database=mls_db;Username=mls_user;******";

    /// <summary>Redis connection string, e.g. <c>redis:6379</c>.</summary>
    public string RedisConnectionString { get; set; } = "redis:6379";

    /// <summary>Inference result cache TTL in seconds (default 30).</summary>
    public int RedisCacheTtlSeconds { get; set; } = 30;

    /// <summary>Base directory where ONNX model files are stored (default <c>/models</c>).</summary>
    public string ModelBasePath { get; set; } = "/models";

    /// <summary>Path to the model-t ONNX file. Empty string disables startup loading.</summary>
    public string ModelTPath { get; set; } = string.Empty;

    /// <summary>Path to the model-a ONNX file. Empty string disables startup loading.</summary>
    public string ModelAPath { get; set; } = string.Empty;

    /// <summary>Path to the model-d ONNX file. Empty string disables startup loading.</summary>
    public string ModelDPath { get; set; } = string.Empty;

    /// <summary>Maximum wall-clock milliseconds allowed for a single inference call (default 50).</summary>
    public int InferenceTimeoutMs { get; set; } = 50;

    /// <summary>Maximum number of concurrent inference calls (default 8).</summary>
    public int MaxConcurrentInferences { get; set; } = 8;
}
