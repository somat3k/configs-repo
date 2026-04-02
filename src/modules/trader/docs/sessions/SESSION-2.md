# trader — Session 2: Key Components

> Use this document as context when generating Trader module code with GitHub Copilot.

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
