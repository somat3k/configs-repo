.PHONY: help infra-up infra-down infra-logs build test lint run new-session new-module

# ─── Variables ───────────────────────────────────────────────────────────────
MODULE ?= undefined
SESSION ?= session-$(shell date +%Y%m%d-%H%M)
COMPOSE = docker compose -f infra/docker-compose.yml

# ─── Help ────────────────────────────────────────────────────────────────────
help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-20s\033[0m %s\n", $$1, $$2}'

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

infra-reset: ## Stop and remove all infra volumes
	$(COMPOSE) down -v

# ─── Rust ────────────────────────────────────────────────────────────────────
build-rust: ## Build all Rust crates
	cargo build --all

test-rust: ## Test all Rust crates
	cargo test --all

lint-rust: ## Lint all Rust crates
	cargo fmt --all -- --check
	cargo clippy --all-targets --all-features -- -D warnings

# ─── Python ──────────────────────────────────────────────────────────────────
install-python: ## Install Python dependencies
	pip install -e ".[dev]"

test-python: ## Run Python tests
	pytest tests/ -v

lint-python: ## Lint Python code
	ruff check .
	mypy src/

# ─── Combined ────────────────────────────────────────────────────────────────
build: build-rust ## Build everything
test:  test-rust test-python ## Run all tests
lint:  lint-rust lint-python ## Lint everything

# ─── Module operations ───────────────────────────────────────────────────────
run: ## Run a module (MODULE=<name>)
	@[ "$(MODULE)" != "undefined" ] || (echo "Usage: make run MODULE=<name>" && exit 1)
	cargo run --bin $(MODULE)

test-module: ## Test a module (MODULE=<name>)
	@[ "$(MODULE)" != "undefined" ] || (echo "Usage: make test-module MODULE=<name>" && exit 1)
	cargo test --bin $(MODULE)

new-module: ## Scaffold a new module (MODULE=<name>)
	@[ "$(MODULE)" != "undefined" ] || (echo "Usage: make new-module MODULE=<name>" && exit 1)
	@bash scripts/new-module.sh $(MODULE)

# ─── Sessions ────────────────────────────────────────────────────────────────
new-session: ## Start a new session (SESSION=<id>)
	@bash scripts/new-session.sh $(SESSION)
