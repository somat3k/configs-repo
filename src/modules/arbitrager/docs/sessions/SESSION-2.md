# arbitrager — Session 2: Key Constraints

> Use this document as context when generating Arbitrager module code with GitHub Copilot.

## Key Constraints

- **NO Uniswap** — use alternative DEX protocols only
- All blockchain addresses loaded from PostgreSQL `blockchain_addresses` table
- Array Builder constructs multi-hop transaction sequences
- ML scorer evaluates opportunity viability before execution
