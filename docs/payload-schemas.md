# Payload Schemas

All WebSocket messages in the MLS platform follow the Envelope Protocol.

## Base Envelope

```typescript
interface Envelope {
  type: string;          // MessageType constant
  version: number;       // Schema version (>= 1)
  session_id: string;    // UUID v4
  module_id: string;     // Sender module ID
  timestamp: string;     // ISO 8601
  payload: object;       // Typed payload (see below)
}
```

## Payload Types

### MODULE_REGISTER
```json
{
  "module_name": "trader",
  "endpoint_http": "http://trader:5300",
  "endpoint_ws": "ws://trader:6300",
  "capabilities": ["trading", "ml-inference"],
  "version": "1.0.0"
}
```

### MODULE_HEARTBEAT
```json
{
  "status": "healthy",
  "uptime_seconds": 3600,
  "metrics": {
    "cpu_percent": 12.5,
    "memory_mb": 256,
    "active_connections": 3,
    "messages_processed": 10540
  }
}
```

### TRADE_SIGNAL
```json
{
  "symbol": "BTC-PERP",
  "side": "BUY",
  "price": 42000.50,
  "quantity": 0.1,
  "confidence": 0.87,
  "model_version": "trader-v2.1",
  "strategy": "momentum",
  "stop_loss": 41500.00,
  "take_profit": 43500.00
}
```

### ARBITRAGE_OPPORTUNITY
```json
{
  "opportunity_id": "uuid",
  "buy_exchange": "hyperliquid",
  "sell_exchange": "broker2",
  "symbol": "ETH-PERP",
  "buy_price": 2200.10,
  "sell_price": 2202.50,
  "spread_bps": 10.9,
  "estimated_profit_usd": 48.00,
  "volume_usd": 10000,
  "confidence": 0.92,
  "expires_at": "2024-01-15T10:30:05.000Z"
}
```

### INFERENCE_REQUEST
```json
{
  "request_id": "uuid",
  "model_name": "trader-signal-v2",
  "features": [1.0, 0.5, -0.3, 0.8, 1.2],
  "feature_names": ["rsi", "macd", "bb_position", "volume_delta", "momentum"],
  "timeout_ms": 50
}
```

### INFERENCE_RESULT
```json
{
  "request_id": "uuid",
  "model_name": "trader-signal-v2",
  "prediction": 0.87,
  "class": "BUY",
  "probabilities": {"BUY": 0.87, "SELL": 0.08, "HOLD": 0.05},
  "inference_ms": 4.2,
  "model_version": "2.1.0"
}
```

<!-- For all SHELL_* messages, `Envelope.session_id` IS the shell session identifier.
     Do not duplicate it inside the payload body. -->

### SHELL_EXEC_REQUEST
```json
{
  "command": "python train.py --model model_t --epochs 50",
  "working_dir": "/app/scripts",
  "env": { "PYTHONPATH": "/app", "MODEL_TYPE": "trading" },
  "timeout_seconds": 600,
  "capture_output": true
}
```

### SHELL_INPUT
```json
{
  "data": "ls -la\n"
}
```

### SHELL_RESIZE
```json
{
  "cols": 220,
  "rows": 50
}
```

### SHELL_OUTPUT
```json
{
  "stream": "stdout",
  "chunk": "Epoch 1/50 — loss: 0.421  acc: 0.873\n",
  "sequence": 42,
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

### SHELL_SESSION_STATE
```json
{
  "previous_state": "Running",
  "current_state": "Completed",
  "exit_code": 0,
  "duration_ms": 4821
}
```

### SHELL_SESSION_CREATED
```json
{
  "label": "model-t training run #14",
  "requesting_module_id": "ml-runtime-550e8400",
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

### SHELL_SESSION_TERMINATED
```json
{
  "label": "model-t training run #14",
  "exit_code": 130,
  "duration_ms": 4821,
  "terminated_by": "timeout",
  "timestamp": "2024-01-15T10:30:05.000Z"
}
```

---

## Designer + AI Hub Envelope Types

> Added as part of the giga-scale platform expansion.
> See [Giga-Scale Plan](architecture/giga-scale-plan.md) for full architecture.

### STRATEGY_DEPLOY
```json
{
  "graph_id": "550e8400-e29b-41d4-a716-446655440001",
  "name": "BTC Momentum Strategy",
  "schema_version": 3,
  "blocks": [
    { "block_id": "uuid", "block_type": "CandleFeedBlock", "parameters": { "symbol": "BTC-PERP", "timeframe": "5m" } },
    { "block_id": "uuid", "block_type": "RSIBlock", "parameters": { "period": 14 } },
    { "block_id": "uuid", "block_type": "ModelTInferenceBlock", "parameters": { "model_id": "model-t", "confidence_threshold": 0.75 } }
  ],
  "connections": [
    { "connection_id": "conn-001", "from_block_id": "uuid", "from_socket": "candle_output", "to_block_id": "uuid", "to_socket": "candle_input" }
  ]
}
```

### STRATEGY_STATE_CHANGE
```json
{
  "strategy_id": "550e8400-e29b-41d4-a716-446655440001",
  "previous_state": "Stopped",
  "current_state": "Running",
  "timestamp": "2026-01-15T10:30:00.000Z"
}
```

### BLOCK_SIGNAL
```json
{
  "block_id": "uuid",
  "strategy_id": "uuid",
  "socket_name": "indicator_output",
  "socket_type": "IndicatorValue",
  "value": 0.72,
  "timestamp": "2026-01-15T10:30:01.000Z"
}
```

### TRAINING_JOB_START
```json
{
  "job_id": "uuid",
  "model_type": "model-t",
  "feature_schema_version": 3,
  "hyperparams": {
    "epochs": 50,
    "batch_size": 256,
    "learning_rate": 0.001,
    "hidden_dims": [128, 64, 32],
    "dropout_rate": 0.2
  },
  "data_range": { "from": "2025-01-01T00:00:00Z", "to": "2026-01-01T00:00:00Z" }
}
```

### TRAINING_JOB_PROGRESS
```json
{
  "job_id": "uuid",
  "epoch": 15,
  "total_epochs": 50,
  "train_loss": 0.312,
  "val_loss": 0.341,
  "accuracy": 0.847,
  "elapsed_ms": 12450,
  "eta_ms": 29050
}
```

### TRAINING_JOB_COMPLETE
```json
{
  "job_id": "uuid",
  "model_type": "model-t",
  "model_id": "model-t-v3.1",
  "onnx_path": "/artifacts/models/model_t_v3.1.onnx",
  "joblib_path": "/artifacts/models/model_t_v3.1.joblib",
  "ipfs_cid": "QmXoypizjW3WknFiJnKLwHCnL72vedxjQkDDP1mXWo6uco",
  "metrics": {
    "f1_macro": 0.843,
    "accuracy": 0.861,
    "val_sharpe": 1.94
  },
  "duration_ms": 41500
}
```

### AI_QUERY
```json
{
  "query": "Show me how the BTC momentum strategy performed last 30 days",
  "provider_override": null,
  "model_override": null,
  "include_canvas_context": true,
  "conversation_history": []
}
```

### AI_RESPONSE_CHUNK
```json
{
  "chunk_index": 7,
  "text": "Here's the BTC chart with trade entry annotations. The strategy generated",
  "is_final": false,
  "function_calls_pending": 1
}
```

### AI_CANVAS_ACTION
```json
{
  "action_type": "OpenPanel",
  "panel_type": "TradingChart",
  "data": { "symbol": "BTC-PERP", "timeframe": "1d" },
  "title": "BTC-PERP Daily (30d)"
}
```

### DATA_COLLECTION_START
```json
{
  "exchange": "hyperliquid",
  "symbol": "BTC-PERP",
  "timeframe": "5m",
  "from": "2026-01-01T00:00:00Z"
}
```

### DATA_GAP_DETECTED
```json
{
  "exchange": "hyperliquid",
  "symbol": "BTC-PERP",
  "timeframe": "5m",
  "gap_start": "2026-03-15T14:00:00Z",
  "gap_end": "2026-03-15T16:30:00Z",
  "missing_candles": 30
}
```

### DATA_GAP_FILLED
```json
{
  "exchange": "hyperliquid",
  "symbol": "BTC-PERP",
  "timeframe": "5m",
  "gap_start": "2026-03-15T14:00:00Z",
  "gap_end": "2026-03-15T16:30:00Z",
  "candles_inserted": 30,
  "duration_ms": 840
}
```

### ARB_PATH_FOUND
```json
{
  "path_id": "uuid",
  "hops": [
    { "from_token": "USDC", "to_token": "WETH", "exchange": "camelot", "price": 0.000238 },
    { "from_token": "WETH", "to_token": "ARB", "exchange": "dfyn", "price": 1520.4 },
    { "from_token": "ARB", "to_token": "USDC", "exchange": "balancer", "price": 1.241 }
  ],
  "input_amount_usd": 10000,
  "estimated_output_usd": 10048.20,
  "gas_estimate_usd": 12.40,
  "net_profit_usd": 35.80,
  "expires_at": "2026-04-02T16:21:28.000Z"
}
```

### DEFI_HEALTH_WARNING
```json
{
  "position_id": "uuid",
  "protocol": "morpho",
  "asset": "WETH",
  "collateral": "USDC",
  "health_factor": 1.18,
  "threshold": 1.2,
  "severity": "Warning",
  "liquidation_price": 2840.50
}
```

### CANVAS_LAYOUT_SAVE
```json
{
  "user_id": "uuid",
  "layout": [
    {
      "window_id": "uuid",
      "panel_type": "TradingChart",
      "title": "BTC-PERP 5m",
      "x": 0, "y": 0, "width": 800, "height": 500,
      "z_index": 1,
      "is_minimized": false,
      "is_maximized": false
    }
  ]
}
```
