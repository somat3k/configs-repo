# arbitrager — Session 3: Required Components

> Use this document as context when generating Arbitrager module code with GitHub Copilot.

## Required Components

- `IOpportunityScanner` — detect price discrepancies across exchanges
- `IOpportunityScorer` — ML model to score opportunity quality
- `IArrayBuilder` — build transaction arrays for arbitrage execution
- `IArbitrageExecutor` — coordinate with Transactions module
