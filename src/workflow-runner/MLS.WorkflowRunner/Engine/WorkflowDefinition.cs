namespace MLS.WorkflowRunner.Engine;

/// <summary>JSON-serializable descriptor of a workflow run.</summary>
public sealed record WorkflowDefinition
{
    /// <summary>Display name of this workflow.</summary>
    [JsonPropertyName("name")] public string Name { get; init; } = "default";

    /// <summary>Number of cycles to execute.</summary>
    [JsonPropertyName("cycles")] public int Cycles { get; init; } = 1;

    /// <summary>Market symbols to include in each step.</summary>
    [JsonPropertyName("symbols")] public string[] Symbols { get; init; } = ["BTC-USDT", "ETH-USDT"];

    /// <summary>Timeframes to include in each step.</summary>
    [JsonPropertyName("timeframes")] public string[] Timeframes { get; init; } = ["1m", "5m", "15m", "1h"];

    /// <summary>Step names to run. Empty = run all default steps.</summary>
    [JsonPropertyName("steps")] public string[] Steps { get; init; } = [];

    /// <summary>Block Controller HTTP base URL.</summary>
    [JsonPropertyName("block_controller_url")] public string BlockControllerUrl { get; init; } = "http://localhost:5100";

    /// <summary>Designer module HTTP base URL.</summary>
    [JsonPropertyName("designer_url")] public string DesignerUrl { get; init; } = "http://localhost:5250";

    /// <summary>Data Layer module HTTP base URL.</summary>
    [JsonPropertyName("data_layer_url")] public string DataLayerUrl { get; init; } = "http://localhost:5700";

    /// <summary>Retrain gate thresholds.</summary>
    [JsonPropertyName("retrain_gate")] public RetrainGateOptions RetrainGate { get; init; } = new();

    /// <summary>Per-step configuration parameters.</summary>
    [JsonPropertyName("step_params")] public Dictionary<string, JsonElement> StepParams { get; init; } = [];
}
