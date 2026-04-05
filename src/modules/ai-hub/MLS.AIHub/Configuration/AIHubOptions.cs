namespace MLS.AIHub.Configuration;

/// <summary>
/// Configuration options for the AI Hub module, bound from <c>appsettings.json</c>
/// under the <c>"AIHub"</c> section.
/// </summary>
public sealed class AIHubOptions
{
    /// <summary>HTTP endpoint of this module, e.g. <c>http://ai-hub:5750</c>.</summary>
    public string HttpEndpoint { get; set; } = "http://ai-hub:5750";

    /// <summary>WebSocket endpoint of this module, e.g. <c>ws://ai-hub:6750</c>.</summary>
    public string WsEndpoint { get; set; } = "ws://ai-hub:6750";

    /// <summary>Block Controller HTTP base URL, e.g. <c>http://block-controller:5100</c>.</summary>
    public string BlockControllerUrl { get; set; } = "http://block-controller:5100";

    /// <summary>Block Controller SignalR hub endpoint.</summary>
    public string BlockControllerHubUrl { get; set; } = "http://block-controller:6100/hubs/block-controller";

    /// <summary>Trader module HTTP base URL.</summary>
    public string TraderUrl { get; set; } = "http://trader:5300";

    /// <summary>Arbitrager module HTTP base URL.</summary>
    public string ArbitragerUrl { get; set; } = "http://arbitrager:5400";

    /// <summary>DeFi module HTTP base URL.</summary>
    public string DeFiUrl { get; set; } = "http://defi:5500";

    /// <summary>ML Runtime module HTTP base URL.</summary>
    public string MlRuntimeUrl { get; set; } = "http://ml-runtime:5600";

    /// <summary>Designer module HTTP base URL.</summary>
    public string DesignerUrl { get; set; } = "http://designer:5250";

    /// <summary>Shell VM module HTTP base URL.</summary>
    public string ShellVmUrl { get; set; } = "http://shell-vm:5950";

    /// <summary>Web App HTTP base URL (for canvas layout queries).</summary>
    public string WebAppUrl { get; set; } = "http://web-app:5200";

    /// <summary>PostgreSQL connection string for user preferences and conversation history.</summary>
    public string PostgresConnectionString { get; set; } = "Host=data-layer;Database=mls;Username=mls;Password=mls";

    /// <summary>Default LLM provider ID when no user preference is set.</summary>
    public string DefaultProvider { get; set; } = "openai";

    /// <summary>Default model ID for the default provider.</summary>
    public string DefaultModel { get; set; } = "gpt-4o";

    /// <summary>Ordered fallback chain of provider IDs.</summary>
    public string[] FallbackChain { get; set; } = ["openai", "anthropic", "groq", "local"];

    /// <summary>Context assembly timeout in milliseconds. Target: &lt; 200ms.</summary>
    public int ContextAssemblyTimeoutMs { get; set; } = 200;

    /// <summary>Maximum envelope history items included in context.</summary>
    public int MaxEnvelopeHistory { get; set; } = 50;

    /// <summary>Provider-specific configuration.</summary>
    public ProviderConfig Providers { get; set; } = new();
}

/// <summary>Per-provider API keys and base URLs.</summary>
public sealed class ProviderConfig
{
    /// <summary>OpenAI configuration.</summary>
    public OpenAIConfig OpenAI { get; set; } = new();

    /// <summary>Anthropic configuration.</summary>
    public AnthropicConfig Anthropic { get; set; } = new();

    /// <summary>Google AI configuration.</summary>
    public GoogleConfig Google { get; set; } = new();

    /// <summary>Groq configuration.</summary>
    public GroqConfig Groq { get; set; } = new();

    /// <summary>OpenRouter configuration.</summary>
    public OpenRouterConfig OpenRouter { get; set; } = new();

    /// <summary>Vercel AI configuration.</summary>
    public VercelAIConfig VercelAI { get; set; } = new();

    /// <summary>Local (Ollama / llama.cpp) configuration.</summary>
    public LocalConfig Local { get; set; } = new();
}

/// <summary>OpenAI provider configuration.</summary>
public sealed class OpenAIConfig
{
    /// <summary>OpenAI API key from environment.</summary>
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>Anthropic provider configuration.</summary>
public sealed class AnthropicConfig
{
    /// <summary>Anthropic API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Anthropic API base URL.</summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
}

/// <summary>Google AI provider configuration.</summary>
public sealed class GoogleConfig
{
    /// <summary>Google AI API key (Gemini).</summary>
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>Groq provider configuration.</summary>
public sealed class GroqConfig
{
    /// <summary>Groq API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Groq API base URL (OpenAI-compatible).</summary>
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";
}

/// <summary>OpenRouter provider configuration.</summary>
public sealed class OpenRouterConfig
{
    /// <summary>OpenRouter API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>OpenRouter API base URL (OpenAI-compatible).</summary>
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
}

/// <summary>Vercel AI provider configuration.</summary>
public sealed class VercelAIConfig
{
    /// <summary>Vercel AI SDK edge endpoint base URL.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Optional API key for the Vercel AI endpoint.</summary>
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>Local (Ollama / llama.cpp) provider configuration.</summary>
public sealed class LocalConfig
{
    /// <summary>Ollama base URL for OpenAI-compatible API.</summary>
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Default local model to use.</summary>
    public string DefaultModel { get; set; } = "llama3";
}
