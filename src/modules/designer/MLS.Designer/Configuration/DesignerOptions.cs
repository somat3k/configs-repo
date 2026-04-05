namespace MLS.Designer.Configuration;

/// <summary>
/// Configuration options for the Designer module, bound from <c>appsettings.json</c>
/// under the <c>"Designer"</c> section.
/// </summary>
public sealed class DesignerOptions
{
    /// <summary>HTTP endpoint of this module, e.g. <c>http://designer:5250</c>.</summary>
    public string HttpEndpoint { get; set; } = "http://designer:5250";

    /// <summary>WebSocket endpoint of this module, e.g. <c>ws://designer:6250</c>.</summary>
    public string WsEndpoint { get; set; } = "ws://designer:6250";

    /// <summary>Block Controller HTTP base URL, e.g. <c>http://block-controller:5100</c>.</summary>
    public string BlockControllerUrl { get; set; } = "http://block-controller:5100";

    /// <summary>ML Runtime HTTP base URL for inference calls, e.g. <c>http://ml-runtime:5600</c>.</summary>
    public string MlRuntimeUrl { get; set; } = "http://ml-runtime:5600";

    /// <summary>Arbitrum One JSON-RPC endpoint used for on-chain price reads.</summary>
    public string ArbitrumRpcUrl { get; set; } = "https://arb1.arbitrum.io/rpc";

    /// <summary>PostgreSQL connection string for the blockchain address book.</summary>
    public string PostgresConnectionString { get; set; } = "Host=data-layer;Database=mls;Username=mls;Password=mls";

    /// <summary>
    /// Block Controller SignalR hub HTTP endpoint for training job dispatch and progress streaming.
    /// The TrainingDispatcher connects here as a client (<c>?clientId=&lt;guid&gt;</c>), and the
    /// SignalR client negotiates and upgrades transports internally as needed.
    /// </summary>
    public string BlockControllerHubUrl { get; set; } = "http://block-controller:6100/hubs/block-controller";
}
