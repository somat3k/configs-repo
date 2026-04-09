> ✅ **Status: Complete** — Implemented and verified in session 23 (workflow-demo).

# Module Network Topology

> **Updated**: Added `designer` (:5250/:6250) and `ai-hub` (:5750/:6750) modules per [Giga-Scale Plan](giga-scale-plan.md).

## Overview

The MLS platform uses a hub-and-spoke network topology with Block Controller as the central orchestration hub. Two new modules — **Designer** and **AI Hub** — were added as part of the giga-scale platform expansion.

## Network Diagram

```mermaid
graph TB
    WA[Web App :5200] -->|SignalR| BC[Block Controller :5100]
    WA -->|AI_QUERY WS| AH[AI Hub :5750]
    AH -->|AI_CANVAS_ACTION SignalR| WA
    AH -->|SK plugin calls| TR
    AH -->|SK plugin calls| DS
    AH -->|SK plugin calls| ML
    AH -->|SK plugin calls| DF
    BC -->|WS :6100| DS[Designer :5250]
    BC -->|WS :6100| TR[Trader :5300]
    BC -->|WS :6100| AR[Arbitrager :5400]
    BC -->|WS :6100| DF[DeFi :5500]
    BC -->|WS :6100| ML[ML Runtime :5600]
    BC -->|WS :6100| DL[Data Layer :5700]
    BC -->|WS :6100| BK[Broker :5800]
    BC -->|WS :6100| TX[Transactions :5900]
    BC -->|WS :6100| SV[Shell VM :5950]

    DS -->|STRATEGY_DEPLOY| BC
    DS -->|TRAINING_JOB_START| SV
    DS -->|queries| DL
    TR -->|queries| DL
    AR -->|queries| DL
    DF -->|queries| TX
    ML -->|inference| TR
    ML -->|inference| AR
    ML -->|exec scripts| SV
    BK -->|orders| DF
    TX -->|blockchain| BK
    WA -->|console| SV

    DL --- PG[(PostgreSQL :5432)]
    DL --- RD[(Redis :6379)]
    DL --- IP[(IPFS :5001)]
    ML --- IP
    SV --- PG
    SV --- RD
```

## Port Allocation

| Module | HTTP API | WebSocket | Container Port | Status |
|--------|----------|-----------|----------------|--------|
| block-controller | 5100 | 6100 | 5100, 6100 | Existing |
| web-app | 5200 | 6200 | 5200, 6200 | Existing |
| **designer** | **5250** | **6250** | **5250, 6250** | **New** |
| trader | 5300 | 6300 | 5300, 6300 | Existing |
| arbitrager | 5400 | 6400 | 5400, 6400 | Existing |
| defi | 5500 | 6500 | 5500, 6500 | Existing |
| ml-runtime | 5600 | 6600 | 5600, 6600 | Existing |
| data-layer | 5700 | 6700 | 5700, 6700 | Existing |
| **ai-hub** | **5750** | **6750** | **5750, 6750** | **New** |
| broker | 5800 | 6800 | 5800, 6800 | Existing |
| transactions | 5900 | 6900 | 5900, 6900 | Existing |
| shell-vm | 5950 | 6950 | 5950, 6950 | Existing |

## Envelope Protocol

All inter-module messages use this JSON envelope:

```json
{
  "type": "TRADE_SIGNAL",
  "version": 1,
  "session_id": "550e8400-e29b-41d4-a716-446655440000",
  "module_id": "trader-550e8400",
  "timestamp": "2024-01-15T10:30:00.000Z",
  "payload": {
    "symbol": "BTC-PERP",
    "side": "BUY",
    "price": 42000.50,
    "confidence": 0.87
  }
}
```

## Message Flow: Trade Execution

```mermaid
sequenceDiagram
    participant DL as Data Layer
    participant ML as ML Runtime
    participant TR as Trader
    participant BC as Block Controller
    participant BK as Broker
    participant TX as Transactions

    DL->>ML: MarketDataUpdate (OHLCV)
    ML->>TR: InferenceResult (signal=BUY, conf=0.87)
    TR->>TR: RiskCheck (position size, stop loss)
    TR->>BC: TradeSignal envelope
    BC->>BK: RouteToExecution
    BK->>TX: CreateTransaction
    TX->>BK: SignedTransaction
    BK-->>TR: OrderConfirmation
    TR->>BC: PositionUpdate
```
