# Arbitrager Module — Session Prompt

## Module Identity
- **Name**: arbitrager
- **Namespace**: `MLS.Arbitrager`
- **HTTP Port**: 5400
- **WebSocket Port**: 6400

## Key Constraints
- **NO Uniswap** — use alternative DEX protocols only
- All blockchain addresses loaded from PostgreSQL `blockchain_addresses` table
- Array Builder constructs multi-hop transaction sequences
- ML scorer evaluates opportunity viability before execution

## Required Components
- `IOpportunityScanner` — detect price discrepancies across exchanges
- `IOpportunityScorer` — ML model to score opportunity quality
- `IArrayBuilder` — build transaction arrays for arbitrage execution
- `IArbitrageExecutor` — coordinate with Transactions module

## Skills to Apply
- `.skills/web3.md` — blockchain address management
- `.skills/machine-learning.md` — ONNX opportunity scorer
- `.skills/beast-development.md` — high-frequency scanning
