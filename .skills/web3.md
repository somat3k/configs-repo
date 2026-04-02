---
name: web3
source: custom (MLS Trading Platform)
description: 'Web3 and blockchain integration for DeFi services — on-chain transactions, wallet connectivity, HYPERLIQUID integration, and smart contract interactions without Uniswap.'
---

# Web3 & DeFi Development — MLS Trading Platform

## Project Constraints
- **NO Uniswap integrations whatsoever** — use alternative DEX protocols only
- Primary DEX/perpetuals: **HYPERLIQUID** as primary, with two fallback brokers
- All blockchain addresses are stored in PostgreSQL and referenced by ID — never hardcoded
- All on-chain transactions go through the **DEFI-services** module
- Use **Nethereum** library for Ethereum/EVM chain interactions in C#

## Supported Chains
- EVM-compatible chains (via Nethereum)
- HYPERLIQUID L1 (via their REST/WebSocket API)
- Define all chain configurations in `MLS.Core.Constants.ChainConstants`

## Wallet & Key Management
- Never store private keys in code or environment variables directly
- Use HSM (Hardware Security Module) or encrypted vault pattern
- Implement `IWalletProvider` interface for pluggable wallet backends
- Support hardware wallet integration via WalletConnect protocol

## Transaction Flow
1. Transaction request created by Trader/Arbitrager module
2. Validated by Transactions System Module
3. Signed by Wallet Provider
4. Broadcast via DEFI-services module
5. Monitored for confirmation by Block Controller
6. Result reported back via Envelope Protocol

## DeFi Services
- Perpetual futures trading on HYPERLIQUID
- On-chain arbitrage via Array Builder module
- Liquidity pool interactions (non-Uniswap only)
- Cross-chain bridging operations

## Blockchain Address Management
- All addresses stored in PostgreSQL `blockchain_addresses` table
- Addresses referenced by named enum: `BlockchainAddress.HyperliquidRouter`
- Address book synchronized from on-chain registry at startup
- Support for address aliasing and labeling

## HYPERLIQUID Integration
- Use official HYPERLIQUID REST API for order management
- Use HYPERLIQUID WebSocket API for real-time order book and fills
- Implement paper trading mode (parallel to live) for backtesting
- Fallback to configured broker endpoints if HYPERLIQUID is unavailable
