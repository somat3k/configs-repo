# MLS AI Hub Module

> **Module identity**: `ai-hub` · HTTP `5750` · WebSocket `6750`

The AI Hub is the multi-provider LLM orchestration layer for the MLS trading platform.
It routes user queries to one of seven LLM providers via a user-defined distributor,
assembles live platform context, and dispatches canvas actions to the Blazor MDI web application
via Semantic Kernel plugins.

---

## Architecture

```
┌──────────────────────── AI Hub Module ───────────────────────────────┐
│                                                                       │
│  ProviderRouter (IProviderRouter)                                     │
│    └── 7 providers: OpenAI · Anthropic · Google · Groq               │
│                     OpenRouter · VercelAI · Local (Ollama)            │
│                                                                       │
│  Semantic Kernel — plugins, function calling, streaming               │
│  BlockControllerClient — MODULE_REGISTER + MODULE_HEARTBEAT (5s)     │
│  AIHubDbContext — user preferences (PostgreSQL)                       │
│                                                                       │
└──────────────────────────────────────────────────────────────────────┘
          │ MODULE_REGISTER / HEARTBEAT
          ▼
   Block Controller (5100/6100)
```

---

## Ports

| Protocol | Port | Endpoint |
|----------|------|----------|
| HTTP API | 5750 | `http://ai-hub:5750` |
| WebSocket / SignalR | 6750 | `ws://ai-hub:6750/hubs/ai-hub` |

---

## LLM Providers

All providers implement `ILLMProvider` and wrap Semantic Kernel's `IChatCompletionService`.

| Provider ID | Display Name | Supported Models | Protocol |
|-------------|-------------|-----------------|---------|
| `openai` | OpenAI | GPT-4o, GPT-4-turbo, o3 | OpenAI REST |
| `anthropic` | Anthropic | Claude 3.5 Sonnet, Claude 3 Opus | Anthropic Messages API |
| `google` | Google AI | Gemini 2.5 Pro, Gemini Flash | SK Google connector |
| `groq` | Groq | Llama3-70b, Mixtral | OpenAI-compatible |
| `openrouter` | OpenRouter | 100+ models | OpenAI-compatible |
| `vercelai` | Vercel AI | Configurable | OpenAI-compatible |
| `local` | Local (Ollama) | llama3, mistral, codellama | OpenAI-compatible |

### Provider Selection Priority

```
1. Per-request override (AI_QUERY.provider_override)
2. User preference primary (PostgreSQL → ai_hub_user_preferences)
3. Fallback chain — probe each candidate in order
4. Local (Ollama) — always available as final fallback
```

### Circuit Breaker

Each provider trips after **3 consecutive failures** and recovers after **60 seconds**.

---

## Configuration

```json
{
  "AIHub": {
    "HttpEndpoint": "http://ai-hub:5750",
    "WsEndpoint": "ws://ai-hub:6750",
    "BlockControllerUrl": "http://block-controller:5100",
    "DefaultProvider": "openai",
    "DefaultModel": "gpt-4o",
    "FallbackChain": ["openai", "anthropic", "groq", "local"],
    "ContextAssemblyTimeoutMs": 200,
    "Providers": {
      "OpenAI": { "ApiKey": "" },
      "Anthropic": { "ApiKey": "", "BaseUrl": "https://api.anthropic.com" },
      "Google": { "ApiKey": "" },
      "Groq": { "ApiKey": "", "BaseUrl": "https://api.groq.com/openai/v1" },
      "OpenRouter": { "ApiKey": "", "BaseUrl": "https://openrouter.ai/api/v1" },
      "VercelAI": { "BaseUrl": "", "ApiKey": "" },
      "Local": { "OllamaBaseUrl": "http://localhost:11434", "DefaultModel": "llama3" }
    }
  }
}
```

---

## Envelope Types

### Consumed

| Type | Action |
|------|--------|
| `AI_QUERY` | Assemble context → select provider → SK invoke |

### Produced

| Type | When |
|------|------|
| `AI_RESPONSE_CHUNK` | Each token chunk in streaming response |
| `AI_CANVAS_ACTION` | Canvas panel/chart action dispatched by plugins |
| `AI_RESPONSE_COMPLETE` | Final token sent |

---

## Persistence

The `ai_hub_user_preferences` PostgreSQL table stores per-user LLM provider selections
and fallback chains. Schema is created automatically on startup via EF Core.

| Column | Type | Description |
|--------|------|-------------|
| `user_id` | UUID (PK) | Platform user identifier |
| `primary_provider_id` | VARCHAR(64) | Preferred provider (e.g. `openai`) |
| `preferred_model_id` | VARCHAR(128) | Preferred model (e.g. `gpt-4o`) |
| `fallback_chain_raw` | VARCHAR(512) | Comma-separated fallback chain |
| `updated_at` | TIMESTAMPTZ | Last update timestamp |

---

## Running

```bash
# Development
dotnet run --project src/modules/ai-hub/MLS.AIHub

# Docker
docker build -f src/modules/ai-hub/Dockerfile -t mls-ai-hub src
docker run -p 5750:5750 -p 6750:6750 mls-ai-hub

# Health check
curl http://localhost:5750/health
```

---

## Tests

```bash
dotnet test src/modules/ai-hub/MLS.AIHub.Tests -c Release
```

**ProviderRouterTests** — 5 tests covering:
- `SelectProvider_FallsBackToLocalWhenPrimaryUnavailable` — circuit-broken primary falls back to Local
- `SelectProvider_UsesPerRequestOverrideWhenSpecified` — per-request override takes priority
- `SelectProvider_UsesUserPreferenceWhenAvailable` — user preference used when provider is up
- `SelectProvider_SkipsUnavailableProvidersInFallbackChain` — walks chain skipping down providers
- `SelectProvider_ThrowsWhenNoProvidersRegistered` — throws when registry is empty
