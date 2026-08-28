# The API of this repository. If you find yourself typing dotnet/go/buf directly,
# the missing target belongs here instead.

.DEFAULT_GOAL := help

# Defaults for the single-service targets below. `run` assumes the C# layout;
# the Go services in month 2 use cmd/server and will need their own recipe.
S ?= order-service
PORT ?= 5001
.PHONY: help setup up infra run call down clean ps logs proto proto-check test arch lint

help: ## Show available targets
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-14s\033[0m %s\n", $$1, $$2}'

setup: ## First-run setup after cloning
	mise install
	cp -n .env.example .env 2>/dev/null || true

## ── Running ──────────────────────────────────────────────────────────

up: ## Start everything (infrastructure + services)
	docker compose -f compose.infra.yml -f compose.services.yml up -d --build

infra: ## Infrastructure only — use when debugging a service from the IDE
	docker compose -f compose.infra.yml up -d

run: ## Run one service from source, no containers (make run S=order-service)
	dotnet run --project services/$(S)/src/Api --no-launch-profile

call: ## Call a running service (make call S=order-service M=rpc.order.v1.OrderService/GetOrder D='{"order_id":"1"}')
	grpcurl -plaintext -import-path proto -proto rpc/order/v1/order_service.proto \
		-d '$(D)' localhost:$(PORT) $(M)

down: ## Stop everything, keep volumes
	docker compose -f compose.infra.yml -f compose.services.yml down

clean: ## Stop everything and DELETE volumes (all data lost)
	docker compose -f compose.infra.yml -f compose.services.yml down -v

ps: ## Container status
	docker compose -f compose.infra.yml -f compose.services.yml ps

logs: ## Follow logs (make logs S=order-service)
	docker compose -f compose.infra.yml -f compose.services.yml logs -f $(S)

## ── Contracts ────────────────────────────────────────────────────────

proto: ## Lint and regenerate all stubs from proto/
	cd proto && buf lint && buf generate

proto-check: ## Detect breaking contract changes against main
	buf breaking proto --against '.git#branch=main,subdir=proto'

## ── Quality ──────────────────────────────────────────────────────────

test: ## Run tests
	dotnet test

arch: ## Architecture tests — enforce service and layer boundaries
	dotnet test tests/arch/dotnet

lint: ## Lint the repository
	cd proto && buf lint
