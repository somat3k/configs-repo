#!/usr/bin/env bash
# new-module.sh — scaffold a new module following Function-as-File conventions
set -euo pipefail

MODULE="${1:?Usage: new-module.sh <module-name>}"
MODULE_DIR="src/modules/$MODULE"

if [ -d "$MODULE_DIR" ]; then
  echo "Module '$MODULE' already exists at $MODULE_DIR"
  exit 1
fi

mkdir -p "$MODULE_DIR/commands"
mkdir -p "$MODULE_DIR/queries"
mkdir -p "$MODULE_DIR/invokers"

# Create main entry point stub
cat > "$MODULE_DIR/main.rs" <<RUST
//! $MODULE module
//!
//! Standalone service. Exposes HTTP + WebSocket on the configured port.
//! Run with: \`make run MODULE=$MODULE\`

use axum::{routing::get, Router};
use std::net::SocketAddr;

#[tokio::main]
async fn main() {
    tracing_subscriber::fmt::init();

    let app = Router::new()
        .route("/health", get(health))
        .route("/info",   get(info));

    let addr: SocketAddr = "0.0.0.0:8000".parse().unwrap();
    tracing::info!("$MODULE listening on {}", addr);
    axum::serve(tokio::net::TcpListener::bind(addr).await.unwrap(), app)
        .await
        .unwrap();
}

async fn health() -> axum::Json<serde_json::Value> {
    axum::Json(serde_json::json!({ "status": "ok", "module": "$MODULE" }))
}

async fn info() -> axum::Json<serde_json::Value> {
    axum::Json(serde_json::json!({
        "module": "$MODULE",
        "version": env!("CARGO_PKG_VERSION"),
    }))
}
RUST

# Create a Dockerfile stub
cat > "$MODULE_DIR/Dockerfile" <<DOCKER
FROM rust:1.78-slim AS builder
WORKDIR /app
COPY . .
RUN cargo build --release --bin $MODULE

FROM debian:bookworm-slim
COPY --from=builder /app/target/release/$MODULE /usr/local/bin/$MODULE
EXPOSE 8000
CMD ["$MODULE"]
DOCKER

echo "✓ Module '$MODULE' scaffolded at $MODULE_DIR"
echo "  Don't forget to:"
echo "  1. Add it to Cargo.toml workspace members"
echo "  2. Add it to infra/docker-compose.yml"
echo "  3. Register it in .structure_pkg.json"
