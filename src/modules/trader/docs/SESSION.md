# Trader Module — Session Prompt

> Use this document as context when generating Trader module code with GitHub Copilot.

## Module Identity
- **Name**: trader
- **Role**: Algorithmic trading model — signal generation and order execution
- **Namespace**: `MLS.Trader`
- **HTTP Port**: 5300
- **WebSocket Port**: 6300
- **Container**: `mls-trader`

## Key Components

### Signal Generation
- Loads ONNX model from ML Runtime
- Features: RSI, MACD, Bollinger Bands, Volume Delta, Momentum
- Output: BUY/SELL/HOLD signal with confidence score
- Inference target: < 10ms

### Order Management
- States: `Draft → Pending → Open → PartiallyFilled → Filled → Cancelled`
- Use `IOrderManager` for all order operations
- Paper trading mode: simulate fills without live execution

### Risk Management
- Position sizing based on Kelly Criterion
- Maximum position size: configurable via `TraderConfig.MaxPositionSizeUsd`
- Stop loss: ATR-based or fixed percentage
- Take profit: Risk:Reward ratio (default 2:1)

## Payload Types Used
- Receives: `INFERENCE_RESULT`, `MARKET_DATA_UPDATE`, `POSITION_UPDATE`
- Sends: `TRADE_SIGNAL`, `ORDER_CREATE`, `ORDER_CANCEL`, `MODULE_HEARTBEAT`

## Skills to Apply
- `.skills/dotnet-devs.md` — C# patterns
- `.skills/machine-learning.md` — ONNX inference
- `.skills/websockets-inferences.md` — WebSocket + inference API
- `.skills/beast-development.md` — Low-latency signal processing
