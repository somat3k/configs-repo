# Trader Module — Algorithmic Trading Model

## Overview
The Trader module is an algorithmic trading engine that generates trade signals using ML inference and executes orders through the Broker module.

## Responsibilities
- Generate trading signals from ML model inference
- Manage order lifecycle (create, modify, cancel)
- Track open positions and P&L
- Risk management (position sizing, stop loss, take profit)
- Report to Block Controller

## Ports
- HTTP API: `5300`
- WebSocket Server: `6300`

## Session prompt: [docs/SESSION.md](docs/SESSION.md)
