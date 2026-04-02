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
