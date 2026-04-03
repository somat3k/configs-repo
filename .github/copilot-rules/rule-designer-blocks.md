# Rule: Designer Block Requirements

> Apply when: generating any code in `MLS.Designer`, implementing `IBlockElement`, modifying strategy schemas, or connecting sockets in the composition graph.

---

## Block Type Requirements

Every block implementation MUST:

1. Implement `IBlockElement` from `MLS.Core.Designer`
2. Register with `IBlockRegistry` using a unique string key matching `BlockType` property
3. Declare all parameters as `BlockParameter<T>` instances in the constructor — no magic values
4. Be `IAsyncDisposable` and clean up all subscriptions and resources in `DisposeAsync`
5. Be zero-allocation on the `ProcessAsync` hot path (use `ArrayPool`, `Span`, pre-allocated state)
6. Have at least one xUnit test with known input → expected output fixture

## Socket Connection Rules

- Sockets MUST be type-safe: `BlockSocketType` must match at both endpoints
- `ICompositionGraph.ConnectAsync` MUST throw `InvalidBlockConnectionException` on type mismatch
- Socket names: inputs `{type}_input`, outputs `{type}_output`
- Multi-input sockets: `{type}_input_a`, `{type}_input_b`

## Strategy Schema Versioning

- `SchemaVersion` MUST be incremented on every structural change (block added/removed, connection changed)
- Minor parameter changes: increment by 1
- Breaking structural changes (socket removal, type change): increment by 10
- Schema version tracked in PostgreSQL temporal table — never lose history

## CompositeBlock Nesting

- `CompositeStrategyBlock` exposes disconnected inner sockets as outer ports via `GetExposedPorts()`
- Nesting is unlimited depth (fractal) — CompositeBlocks can contain CompositeBlocks
- Inner connections are hidden from the outer graph — only exposed ports are visible

## Exchange Adapter Requirements

- All blockchain addresses via `IBlockchainAddressBook` enum — zero hardcoded strings
- `BlockchainAddress` enum values defined in `MLS.Core.Constants.BlockchainAddress`
- NO Uniswap: see `.github/copilot-rules/rule-no-uniswap.md` — absolute prohibition
- Supported exchanges: HyperLiquid, Camelot, DFYN, Balancer, Morpho (Arbitrum network only)

## Training Block Requirements

- `TrainModelBlock` emits `TRAINING_JOB_START` envelope — never calls Python directly
- Feature schema version in `TRAINING_JOB_START` MUST match `FeatureSchema.Version` in `MLS.Core`
- `ExportONNXBlock` MUST upload to IPFS AND register in `model_registry` PostgreSQL table
- Model artifact naming: `model_t_{version}.onnx` / `model_a_{version}.onnx` / `model_d_{version}.onnx`
