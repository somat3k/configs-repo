# Data Layer Module — Data-Driven Access Layer

## Overview
The Data Layer module is the Layer-0 data hub. It ingests, transforms, and distributes all market data and serves as the persistence gateway to PostgreSQL, Redis, and IPFS.

## Responsibilities
- Real-time market data ingestion from external feeds
- Feature computation and storage
- Data transformation and augmentation pipelines
- Redis cache management
- IPFS artifact storage
- Data distribution to all subscribed modules

## Ports: HTTP 5700 / WebSocket 6700
## Session prompt: [docs/SESSION.md](docs/SESSION.md)
