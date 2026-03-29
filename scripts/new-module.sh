#!/usr/bin/env bash
# new-module.sh — scaffold a new module following Function-as-File conventions
set -euo pipefail

MODULE="${1:?Usage: new-module.sh <module-name>}"
WORKSPACE_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MODULE_DIR="$WORKSPACE_ROOT/src/modules/$MODULE"

if [ -d "$MODULE_DIR" ]; then
  echo "Module '$MODULE' already exists at $MODULE_DIR"
  exit 1
fi

mkdir -p "$MODULE_DIR/commands"
mkdir -p "$MODULE_DIR/queries"
mkdir -p "$MODULE_DIR/invokers"
mkdir -p "$MODULE_DIR/src"

# ─── Cargo.toml ──────────────────────────────────────────────────────────────
cat > "$MODULE_DIR/Cargo.toml" << TOML
[package]
name = "$MODULE"
version = "0.1.0"
edition = "2021"

[[bin]]
name = "$MODULE"
path = "src/main.rs"

[dependencies]
axum              = { workspace = true }
tokio             = { workspace = true }
serde             = { workspace = true }
serde_json        = { workspace = true }
tracing           = { workspace = true }
tracing-subscriber = { workspace = true }
anyhow            = { workspace = true }
dotenvy           = { workspace = true }
TOML

# ─── src/main.rs ─────────────────────────────────────────────────────────────
cat > "$MODULE_DIR/src/main.rs" << 'RUST'
//! MODULE_NAME module
//!
//! Standalone service. Exposes HTTP on MODULE_PORT (env var) or 8000.
//! Run with: `make run MODULE=MODULE_NAME`

use axum::{
    extract::State,
    http::StatusCode,
    routing::{get, post},
    Json, Router,
};
use serde::{Deserialize, Serialize};
use serde_json::{json, Value};
use std::net::SocketAddr;

/// Typed Envelope for all inter-module messages.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Envelope {
    #[serde(rename = "type")]
    pub msg_type: String,
    pub version: u32,
    pub session_id: String,
    pub payload: Value,
}

#[derive(Clone)]
struct AppState {
    module_name: String,
}

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    dotenvy::dotenv().ok();
    tracing_subscriber::fmt()
        .with_env_filter(std::env::var("LOG_LEVEL").unwrap_or_else(|_| "info".into()))
        .init();

    let port: u16 = std::env::var("MODULE_PORT")
        .unwrap_or_else(|_| "8000".into())
        .parse()
        .unwrap_or(8000);

    let state = AppState {
        module_name: "MODULE_NAME".to_string(),
    };

    let app = Router::new()
        .route("/health", get(health))
        .route("/info",   get(info))
        .route("/invoke", post(invoke))
        .with_state(state);

    let addr = SocketAddr::from(([0, 0, 0, 0], port));
    tracing::info!("MODULE_NAME listening on {}", addr);

    let listener = tokio::net::TcpListener::bind(addr).await
        .map_err(|e| anyhow::anyhow!("Failed to bind {}: {}", addr, e))?;
    axum::serve(listener, app).await
        .map_err(|e| anyhow::anyhow!("Server error: {}", e))
}

async fn health(State(s): State<AppState>) -> Json<Value> {
    Json(json!({ "status": "ok", "module": s.module_name }))
}

async fn info(State(s): State<AppState>) -> Json<Value> {
    Json(json!({
        "module":  s.module_name,
        "version": env!("CARGO_PKG_VERSION"),
        "endpoints": ["/health", "/info", "/invoke"],
    }))
}

async fn invoke(
    State(_s): State<AppState>,
    Json(envelope): Json<Envelope>,
) -> Result<Json<Value>, (StatusCode, Json<Value>)> {
    // TODO: route envelope.msg_type to the appropriate command/query handler
    tracing::info!(msg_type = %envelope.msg_type, "received envelope");
    Err((
        StatusCode::NOT_IMPLEMENTED,
        Json(json!({
            "type": "Error",
            "version": 1,
            "session_id": envelope.session_id,
            "payload": { "message": "invoke not yet implemented" }
        })),
    ))
}
RUST

# Replace MODULE_NAME placeholder in main.rs
sed -i.bak "s/MODULE_NAME/$MODULE/g" "$MODULE_DIR/src/main.rs"
rm -f "$MODULE_DIR/src/main.rs.bak"

# ─── Dockerfile ──────────────────────────────────────────────────────────────
cat > "$MODULE_DIR/Dockerfile" << DOCKER
FROM rust:1.78-slim AS builder
WORKDIR /app
COPY . .
RUN cargo build --release --manifest-path src/modules/$MODULE/Cargo.toml --bin $MODULE

FROM debian:bookworm-slim
COPY --from=builder /app/target/release/$MODULE /usr/local/bin/$MODULE
ENV MODULE_PORT=8000
EXPOSE \${MODULE_PORT}
CMD ["$MODULE"]
DOCKER

echo "✓ Module '$MODULE' scaffolded at $MODULE_DIR"
echo ""
echo "  Next steps:"
echo "  1. Add '$MODULE' to Cargo.toml workspace members"
echo "  2. Add service entry to infra/docker-compose.yml"
echo "  3. Register in .structure_pkg.json → modules[]"
echo "  4. Set MODULE_PORT in .env"
echo "  5. make run MODULE=$MODULE"
