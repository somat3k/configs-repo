.PHONY: help \
        infra-up infra-down infra-logs infra-reset \
        build-rust test-rust lint-rust \
        install-python test-python lint-python \
        solidity-compile solidity-test \
        build test lint \
        run build-module test-module \
        new-module new-session

# ─── Variables ───────────────────────────────────────────────────────────────
MODULE  ?= undefined
SESSION ?= session-$(shell date +%Y%m%d-%H%M)
COMPOSE  = docker compose -f infra/docker-compose.yml

# Guard: require MODULE to be set
_require-module:
	@[ "$(MODULE)" != "undefined" ] || \
		(echo "Usage: make $(MAKECMDGOALS) MODULE=<name>" && exit 1)

# ─── Help ────────────────────────────────────────────────────────────────────
help: ## Show available targets
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-22s\033[0m %s\n", $$1, $$2}'

# ─── Infrastructure ──────────────────────────────────────────────────────────
infra-up: ## Start infrastructure (Redis, Postgres, IPFS)
	$(COMPOSE) up -d
	@echo "Waiting for services to be healthy..."
	@sleep 3
	$(COMPOSE) ps

infra-down: ## Stop infrastructure
	$(COMPOSE) down

infra-logs: ## Tail infrastructure logs
	$(COMPOSE) logs -f

infra-reset: ## Stop infrastructure and remove all volumes
	$(COMPOSE) down -v

# ─── Rust ────────────────────────────────────────────────────────────────────
build-rust: ## Build all Rust workspace crates
	@if cargo metadata --no-deps --format-version 1 2>/dev/null | python3 -c "import json,sys; exit(0 if json.load(sys.stdin)['packages'] else 1)" 2>/dev/null; then \
		cargo build --all; \
	else \
		echo "No Rust workspace members found — skipping build."; \
	fi

test-rust: ## Test all Rust workspace crates
	@if cargo metadata --no-deps --format-version 1 2>/dev/null | python3 -c "import json,sys; exit(0 if json.load(sys.stdin)['packages'] else 1)" 2>/dev/null; then \
		cargo test --all --all-features; \
	else \
		echo "No Rust workspace members found — skipping tests."; \
	fi

lint-rust: ## Lint all Rust workspace crates (fmt + clippy)
	@if cargo metadata --no-deps --format-version 1 2>/dev/null | python3 -c "import json,sys; exit(0 if json.load(sys.stdin)['packages'] else 1)" 2>/dev/null; then \
		cargo fmt --all -- --check && \
		cargo clippy --all-targets --all-features -- -D warnings; \
	else \
		echo "No Rust workspace members found — skipping lint."; \
	fi

# ─── Python ──────────────────────────────────────────────────────────────────
install-python: ## Install Python dependencies (including dev extras)
	pip install -e ".[dev]"

test-python: ## Run Python tests
	pytest -v

lint-python: ## Lint Python code (ruff + mypy)
	ruff check .
	@if [ -d src ]; then mypy src/; else echo "No src/ directory — skipping mypy."; fi

# ─── Solidity (Python / Ape Framework) ───────────────────────────────────────
solidity-compile: ## Compile Solidity contracts via Ape Framework
	@if [ -d src/contracts ]; then \
		ape compile; \
	else \
		echo "No src/contracts/ directory — skipping compile."; \
	fi

solidity-test: ## Test Solidity contracts via Ape Framework
	@if [ -d tests/contracts ]; then \
		ape test tests/contracts/ -v; \
	else \
		echo "No tests/contracts/ directory — skipping."; \
	fi

# ─── Combined ────────────────────────────────────────────────────────────────
build: build-rust ## Build all (extend as new language modules are added)
test:  test-rust test-python ## Run all tests
lint:  lint-rust lint-python ## Lint everything

# ─── Module operations ───────────────────────────────────────────────────────
run: _require-module ## Run a module by name: make run MODULE=<name>
	cargo run --bin $(MODULE)

build-module: _require-module ## Build a single module: make build-module MODULE=<name>
	cargo build --bin $(MODULE)

test-module: _require-module ## Test a single module: make test-module MODULE=<name>
	cargo test --bin $(MODULE)

new-module: ## Scaffold a new module: make new-module MODULE=<name>
	@[ "$(MODULE)" != "undefined" ] || \
		(echo "Usage: make new-module MODULE=<name>" && exit 1)
	@bash scripts/new-module.sh $(MODULE)

# ─── Sessions ────────────────────────────────────────────────────────────────
new-session: ## Start a new session: make new-session SESSION=<id>
	@bash scripts/new-session.sh $(SESSION)
