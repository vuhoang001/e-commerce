# ecommerce-polyglot — Build Plan

> A **polyglot** e-commerce microservices system: C# (business logic) · Go (throughput) · Python (AI/data) · Java (streaming).
> **Contract-first** communication over gRPC (sync) and Kafka (async).
>
> - **Duration:** 6 months
> - **Dual goal — two career tracks, equal weight:** **Backend / Platform Engineer** and **Data Engineer (Streaming)**.
>   Every phase is scored against both. See [section 17](#17-dual-track-plan--backend-and-data-engineer).
> - **Created:** 2026-08-28
> - **Project language: English.** All docs, code comments, commit messages, and ADRs are written in English.

---

## Table of Contents

1. [Review of the proposed structure](#1-review-of-the-proposed-structure)
2. [Complete structure](#2-complete-structure)
3. [Service breakdown — which language, and why](#3-service-breakdown--which-language-and-why)
3b. [Gateway on C# + YARP — what it can and cannot do](#3b-gateway-on-c--yarp--what-it-can-and-cannot-do)
4. [Contract-first workflow](#4-contract-first-workflow)
5. [Communication rule: gRPC or Kafka](#5-communication-rule-grpc-or-kafka)
6. [Local development experience](#6-local-development-experience)
7. [Cross-language observability](#7-cross-language-observability)
8. [Six-month roadmap](#8-six-month-roadmap)
9. [CI/CD for a polyglot monorepo](#9-cicd-for-a-polyglot-monorepo)
10. [Known pitfalls](#10-known-pitfalls)
11. [Out of scope](#11-out-of-scope)
12. [Interview map](#12-interview-map)
13. [Progress tracker](#13-progress-tracker)
14. [Comparison with BookWorm](#14-comparison-with-bookworm)
15. [Agent skills — pinning AI guidance into the repo](#15-agent-skills--pinning-ai-guidance-into-the-repo)
16. [Batch pipeline — master data ingestion](#16-batch-pipeline--master-data-ingestion)
17. [Dual-track plan — backend and data engineer](#17-dual-track-plan--backend-and-data-engineer)
18. [Order items and master data — copy or look up](#18-order-items-and-master-data--copy-or-look-up)

---

## 1. Review of the proposed structure

### Right as-is — keep

| Decision | Why it's right |
|---|---|
| `proto/` at the **repo root** | Contracts are shared property; they belong to no single service. Putting them at the root forces every contract change to show up plainly in the PR diff. |
| A `Dockerfile` per service | Mandatory for polyglot. Each language has a different base image and build stage. |
| `building-blocks/` | Good name (borrowed from eShopOnContainers). This is where infrastructure code stops being duplicated. |
| Different *internal* layout per language | Correct. `Domain/Application/Infrastructure` for C#, `cmd/internal` for Go — **don't force one mould**; each community has its own conventions. |

### Six things to fix

| # | Problem | Fix |
|---|---|---|
| 1 | **`src/services/order-service/src/`** — `src` nested twice | Drop the outer `src/`. Top level becomes `services/`, `proto/`, `building-blocks/`. One level shorter everywhere. |
| 2 | **Go has both `cmd/` and a root `main.go`** | Conflicting conventions. Pick `cmd/server/main.go`; no `main.go` at the service root. |
| 3 | **Nowhere for proto-generated code to land** | `buf generate` has to write somewhere. → `building-blocks/gen/{csharp,go,python,java}/` |
| 4 | **gRPC only, no event schemas** | E-commerce **requires** async. Add `proto/events/` for Kafka messages. |
| 5 | **Missing `deploy/`, `docs/`, `tests/`, `tools/`** | Without an ADR directory nobody knows what you weighed up. Without `tests/e2e/` there's no proof the system actually runs. |
| 6 | **One `docker-compose.yml` for everything** | This starts hurting in month 2. Split it: `compose.infra.yml` (Kafka, Postgres, Redis…) starts once and stays up; `compose.services.yml` restarts constantly. |

### One architectural question that's missing

You have **three services and a web client**. Does the client call all three directly, or go through one door?
→ You need an **`api-gateway`** (C# / .NET + YARP). Without it the browser has to speak gRPC (it can't directly), CORS is handled three times, and auth is implemented three times.

> ⚠️ **Important technical caveat — see [section 3b](#3b-gateway-on-c--yarp--what-it-can-and-cannot-do).** YARP does **not** translate REST ↔ gRPC. It is an HTTP reverse proxy. The translation has to happen another way, and that changes the gateway's role.

---

## 2. Complete structure

```
ecommerce-polyglot/
├── .agents/
│   └── skills/                     # ★ guidance for AI coding agents — see section 15
│       ├── kafka-conventions/      #   authored here
│       ├── proto-contract/         #   authored here
│       ├── flink-job/              #   authored here
│       ├── go-service/             #   authored here
│       ├── ddd-dotnet/             #   authored here
│       ├── aspire/                 #   vendored from Microsoft
│       └── csharp-tunit/           #   vendored
├── AGENTS.md                       # points every agent at CLAUDE.md
├── CLAUDE.md                       # repo-wide rules: which command to build/test/lint with
├── mise.toml                       # ★ PINS .NET / Go / Python / Java / buf versions
├── Makefile                        # the single entry point: make up / make proto / make test
├── compose.infra.yml               # Kafka, Postgres, Redis, OpenSearch, MinIO, OTel
├── compose.services.yml            # 6 services + gateway
├── .env.example
├── .editorconfig
│
├── proto/                          # ★ THE SINGLE SOURCE OF TRUTH for every contract
│   ├── buf.yaml                    # lint + breaking-change rules
│   ├── buf.gen.yaml                # generates code for all four languages
│   ├── rpc/                        # ── gRPC: request/response ──
│   │   ├── order/v1/order_service.proto
│   │   ├── search/v1/search_service.proto
│   │   ├── recommendation/v1/recommendation_service.proto
│   │   └── inventory/v1/inventory_service.proto
│   ├── events/                     # ── Kafka: things that already happened ──
│   │   ├── order/v1/order_placed.proto
│   │   ├── order/v1/order_cancelled.proto
│   │   ├── catalog/v1/product_updated.proto
│   │   └── user/v1/click_recorded.proto
│   └── common/v1/
│       ├── money.proto
│       └── pagination.proto
│
├── services/
│   ├── order-service/              # [C# / .NET 10]  DDD, transactions, saga
│   │   ├── src/
│   │   │   ├── Domain/             # aggregates, value objects — zero dependencies
│   │   │   ├── Application/        # command/query handlers, MediatR
│   │   │   ├── Infrastructure/     # EF Core, Kafka producer, outbox
│   │   │   └── Api/                # gRPC service implementation + host
│   │   ├── tests/
│   │   │   ├── Domain.UnitTests/
│   │   │   ├── Api.IntegrationTests/
│   │   │   └── ContractTests/      # lives WITH the service that owns the contract
│   │   ├── migrations/
│   │   └── Dockerfile
│   │
│   ├── payment-service/            # [C# / .NET 10]  saga participant
│   │   └── ... (same shape, smaller)
│   │
│   ├── catalog-service/            # [Go]  owns products + serves search
│   │   ├── cmd/server/main.go
│   │   ├── internal/
│   │   │   ├── handler/            # gRPC handlers
│   │   │   ├── indexer/            # consume Kafka → OpenSearch
│   │   │   ├── search/             # query building, ranking
│   │   │   └── config/
│   │   ├── go.mod
│   │   └── Dockerfile
│   │
│   ├── inventory-service/          # [Go]  reserve/release stock under contention
│   │   ├── cmd/server/main.go
│   │   ├── internal/
│   │   └── Dockerfile
│   │
│   ├── recommendation-service/     # [Python 3.12]  AI / embeddings
│   │   ├── app/
│   │   │   ├── grpc_server.py
│   │   │   ├── models/             # embeddings, ANN index
│   │   │   ├── pipelines/          # batch retraining
│   │   │   └── consumers/          # Kafka → feature updates
│   │   ├── pyproject.toml
│   │   └── Dockerfile
│   │
│   ├── stream-processor/           # [Java 21 + Flink 2.x]  ← added in month 4
│   │   ├── jobs/
│   │   │   ├── revenue-rollup/
│   │   │   ├── fraud-detection/
│   │   │   └── sessionization/
│   │   ├── pom.xml
│   │   └── Dockerfile
│   │
│   └── api-gateway/                # [C# / .NET 10 + YARP]  BFF, auth, rate limiting
│       ├── src/                    # ★ NO Api/ level — the gateway has no layers
│       │   ├── Orders/             # ★ one folder per public feature: the endpoint,
│       │   │                       #   its response records and its mapping, together
│       │   ├── Products/           #   month 3 — BFF fan-out, self-contained
│       │   ├── Clients/            # gRPC clients — cross-cutting, so stays technical
│       │   ├── Middleware/         # auth, rate limiting, correlation id
│       │   ├── Program.cs          # YARP routes + OTel
│       │   └── appsettings.json    # beside the .csproj — .NET resolves it from the
│       │                           #   project's content root, not the service root
│       ├── tests/
│       └── Dockerfile

> The gateway groups by **feature**, order-service groups by **layer**. That is not an
> inconsistency: order-service has four layers with enforced dependency rules, and the
> gateway has none. Grouping by technical role in a gateway means one new public feature
> touches four folders. See ADR-004.
│
├── building-blocks/
│   ├── gen/                        # ★ generated from proto — NEVER hand-edited
│   │   ├── csharp/  go/  python/  java/
│   │
│   ├── chassis-dotnet/             # ★ microservices chassis (C#)
│   │   ├── Cqrs/                   #   MediatR + pipeline behaviours
│   │   ├── Endpoints/              #   Minimal API conventions, versioning
│   │   ├── EventBus/               #   Kafka producer/consumer, outbox, DLQ
│   │   ├── Persistence/            #   EF Core base, UnitOfWork, soft delete
│   │   ├── Caching/                #   Redis, cache-aside
│   │   ├── Security/               #   JWT, authorization policies
│   │   ├── Observability/          #   OTel tracing, metrics, structured logging
│   │   ├── Exceptions/             #   ProblemDetails, exception filters
│   │   ├── Validation/             #   FluentValidation conventions
│   │   └── Resilience/             #   Polly: retry, circuit breaker, timeout
│   │
│   ├── chassis-go/                 # same ten concerns, written the Go way
│   │   ├── kafka/  otel/  httpx/  config/  logx/  resilience/
│   │
│   ├── chassis-python/
│   │   └── kafka/  otel/  config/
│   │
│   └── scripts/
│       ├── gen-proto.sh
│       ├── init-databases.sh
│       └── seed-data.py
│
├── data-platform/                  # ★ the BATCH pipeline — see section 16
│   ├── orchestration/
│   │   ├── dags/                   #   Airflow DAGs
│   │   └── plugins/
│   ├── ingestion/                  #   one extractor per source system
│   │   ├── supplier_catalogue/     #     daily snapshot (object storage)
│   │   ├── partner_pricing/        #     incremental REST pull
│   │   └── reference_data/         #     small static reloads
│   ├── transformations/            #   dbt: bronze → silver → gold
│   │   ├── models/bronze/
│   │   ├── models/silver/          #     cleaned, typed, SCD Type 2
│   │   └── models/gold/            #     serving shapes
│   ├── quality/                    #   data quality suites — the gate before gold
│   ├── publishers/                 #   gold → log-compacted Kafka topics
│   └── tests/
│
├── deploy/
│   ├── k8s/
│   ├── connectors/                 # Debezium configuration
│   └── otel/                       # OpenTelemetry Collector configuration
│
├── tests/
│   ├── e2e/                        # k6 or Playwright — driven through the gateway
│   └── arch/                       # ★ architecture tests — enforce boundaries, run in CI
│       ├── dotnet/                 #   NetArchTest / ArchUnitNET
│       ├── go/                     #   go-arch-lint
│       └── python/                 #   import-linter
│                                   # (contract tests live INSIDE each service)
│
├── docs/                           # ★ arc42 skeleton — the standard architecture doc template
│   ├── 01-introduction-and-goals.md
│   ├── 02-architecture-constraints.md
│   ├── 03-context-and-scope.md
│   ├── 04-solution-strategy.md
│   ├── 05-building-block-view.md
│   ├── 06-runtime-view.md          #   ← saga and event flows are drawn here
│   ├── 07-deployment-view.md
│   ├── 08-cross-cutting-concepts/  #   ← topic papers become appendices here
│   │   ├── event-catalog.md
│   │   ├── partition-strategy.md
│   │   ├── schema-evolution.md
│   │   ├── observability.md
│   │   └── service-boundaries.md
│   ├── 09-architecture-decisions/  #   ← ADRs live here (arc42 chapter 9)
│   ├── 10-quality-requirements.md
│   ├── 11-risks-and-technical-debt.md
│   ├── 12-glossary.md
│   ├── failure-modes.md
│   └── runbook.md
│
└── tools/
    └── devcontainer/
```

**Biggest changes from the original sketch:** drop the outer `src/`, split `proto/rpc` from `proto/events`, add `building-blocks/gen/`, add `api-gateway`.

### Three structural decisions borrowed from BookWorm

| Decision | Why |
|---|---|
| **`mise.toml` at the root** | You have **four toolchains**. Without pinned versions, within a month the repo only runs on your machine. This is the second most important file after `Makefile`. |
| **`docs/` following arc42** | Replaces five scattered `.md` files with the industry-standard 12-chapter skeleton. The topic papers (event catalog, partition strategy) become **appendices to chapter 08** — nothing is lost. ADRs land where they belong: **chapter 09**. |
| **Contract tests INSIDE the service** | `services/order-service/tests/ContractTests/`, not a shared `tests/contract/`. A contract test belongs to the service that owns the contract — the same principle as "the schema belongs to the producer". |

### `mise.toml` — write it in week one

```toml
[tools]
dotnet  = "10.0"
go      = "1.24"
python  = "3.12"
java    = "temurin-21"
buf     = "latest"
k6      = "latest"

[env]
_.file = ".env"

[tasks.up]
run = "docker compose -f compose.infra.yml -f compose.services.yml up -d --build"
```

> `mise install` gets all four toolchains on the right version. No more "install .NET 10 first" line in the README.

---

## 3. Service breakdown — which language, and why

Polyglot only pays off when every choice has a **real technical reason**. This table is your answer when an interviewer asks *"why not write the whole thing in one language?"*

| Service | Language | The real reason | What you'd lose otherwise |
|---|---|---|---|
| **order-service** | C# / .NET | The most complex business logic: aggregates, invariants, transactions, saga. Strongest type system, EF Core, best refactoring tooling. | In Go: weaker modelling, thin ORM story, DDD code turns verbose |
| **payment-service** | C# | Shares a language boundary with order, reuses `chassis-dotnet` | — |
| **catalog-service** | Go | Owns product master data and serves search. Read-heavy, I/O-bound, high QPS, p99 latency matters. Goroutines + a 15 MB binary + <100 ms startup make autoscaling cheap. | In C#: slower cold start, 3–4× the RAM for the same throughput |
| **inventory-service** | Go | High contention (many buyers racing for one SKU). Needs low-level concurrency control. | — |
| **recommendation-service** | Python | The ML ecosystem: `sentence-transformers`, `faiss`, `pandas`, `pgvector`. No other language substitutes for it. | In C#: ML.NET exists, but the ecosystem is an order of magnitude thinner |
| **stream-processor** | Java | Flink is Java-native. DataStream API + Flink SQL. | PyFlink: slower, feature-lagging. Flink.NET: nobody uses it, nobody interviews on it |
| **api-gateway** | C# / .NET + YARP | Same toolchain as order/payment → shares `chassis-dotnet`. YARP is built by the ASP.NET team and integrates directly with Aspire, OTel and auth. BFF aggregation in LINQ is far more pleasant than in Go. | In Go: smaller image (~15 MB vs ~110 MB) and faster cold start — at the cost of maintaining a second building-blocks stack |

> **Said plainly:** polyglot has a real cost — four toolchains, four logging styles, four CI jobs, hard-to-share code. In a real company this is usually the **wrong** call unless the reason is clear.
> For a portfolio it is justified, because the goal *is* demonstrating multi-platform capability. Write that down in `docs/09-architecture-decisions/001-why-polyglot.md` — admitting the trade-off makes you more credible than pretending it doesn't exist.

---

## 3b. Gateway on C# + YARP — what it can and cannot do

> Read this **before** writing the first line of gateway code. Switching Go → C# is not a rename; it changes how the gateway works.

### The common misconception

**YARP does not translate REST ↔ gRPC.** YARP is an **HTTP → HTTP** reverse proxy. It routes, load-balances, transforms headers, retries — but it knows nothing about Protobuf. Send `GET /v1/orders/123` through YARP and what arrives at the destination is still an HTTP request, not a gRPC call.

`grpc-gateway` (Go), by contrast, **does** generate a REST↔gRPC proxy from `google.api.http` annotations in the proto. That is the capability you just traded away.

### Three ways to get it back in .NET

| Option | Mechanism | The catch |
|---|---|---|
| **A. gRPC JSON transcoding in each service** | `Microsoft.AspNetCore.Grpc.JsonTranscoding` reads `google.api.http` annotations and exposes REST alongside gRPC. YARP just routes. | **.NET only.** `catalog-service` and `inventory-service` are Go, so they can't use it. You'd add `grpc-gateway` for the two Go services — back to two parallel mechanisms. |
| **B. Gateway holds gRPC clients, exposes Minimal API** ★ | The gateway references proto-generated stubs, calls services over gRPC, and exposes REST through hand-written Minimal API endpoints. | Endpoints are hand-written. But the number of genuinely public endpoints is far smaller than the number of RPCs. |
| **C. Envoy in front, YARP behind** | Envoy does gRPC-JSON transcoding, YARP does auth/BFF | Two proxy layers — complexity this project doesn't earn |

### Recommendation: option B

```csharp
// services/api-gateway/src/Endpoints/OrderEndpoints.cs
app.MapGet("/api/orders/{id:guid}", async (
        Guid id,
        OrderService.OrderServiceClient orders,   // stub generated from proto/rpc/order/v1
        CancellationToken ct) =>
{
    var reply = await orders.GetOrderAsync(
        new GetOrderRequest { OrderId = id.ToString() }, cancellationToken: ct);
    return Results.Ok(reply.ToDto());
})
.RequireAuthorization()
.RequireRateLimiting("per-user");
```

**Why option B is better than it first looks:**

1. **The gateway becomes a real BFF, not a dumb proxy.** `GET /api/products/{id}` can fan out to `search`, `recommendation` and `inventory` in parallel and merge the results — exactly what the frontend needs, instead of making the browser issue three calls.
2. **The contract stays under control.** gRPC stubs are still generated from `proto/`, and `buf breaking` still blocks breaking changes. Only the REST layer is hand-written.
3. **End-to-end type safety.** Change a proto field and the gateway stops compiling. With `grpc-gateway`, that failure surfaces at runtime.

**So what does YARP actually do in option B?**

Not the gRPC endpoints — the things that only need to pass straight through:

```jsonc
// appsettings.json
"ReverseProxy": {
  "Routes": {
    "catalog-passthrough": {                  // catalog-service exposes REST via its own grpc-gateway
      "ClusterId": "catalog",
      "Match": { "Path": "/api/catalog/{**catch-all}" },
      "AuthorizationPolicy": "authenticated",
      "RateLimiterPolicy": "per-user"
    }
  },
  "Clusters": {
    "catalog": { "Destinations": { "d1": { "Address": "http://catalog-service:8080" } } }
  }
}
```

→ **A hybrid model:** YARP for simple passthrough, Minimal API + gRPC clients wherever aggregation or translation is needed. That is YARP used for what it actually is.

### What you gain and lose switching Go → C#

| | Gain | Loss |
|---|---|---|
| Toolchain | One fewer building-blocks stack — the gateway shares logging/otel/auth with order & payment | — |
| Aspire | The gateway joins the `AppHost`, gets service discovery, no hard-coded URLs | — |
| BFF aggregation | LINQ + `async`/`await` + records — markedly easier than Go | — |
| Auth | `Microsoft.AspNetCore.Authentication.JwtBearer` is more mature than the Go equivalents | — |
| REST↔gRPC | — | No more `grpc-gateway` code generation → hand-written Minimal API |
| Runtime | — | ~110 MB image (vs ~15 MB), ~200–400 ms cold start (vs <50 ms), ~60 MB idle RAM (vs ~10 MB) |
| Languages in the repo | Still **four** (C#, Go, Python, Java) — Go remains for search/inventory | — |

> **Worth an ADR:** those runtime numbers only matter once the gateway scales to dozens of instances. At this project's size they don't, and toolchain consolidation with order/payment is worth more in practice. But say explicitly that you **know** the trade-off rather than ignoring it.

### Roadmap consequences

- **Month 0.5:** `api-gateway` (C#) with one Minimal API endpoint calling a gRPC client → order-service
- **Month 2:** add a YARP passthrough route for `catalog-service` (Go, with its own `grpc-gateway`)
- **Month 3:** turn the gateway into a real BFF — `GET /api/products/{id}` fans out to `search` + `recommendation` + `inventory`
- **Month 5:** the gateway is the **origin of the distributed trace** — where the first `traceparent` is minted

---

## 4. Contract-first workflow

This is what keeps a polyglot monorepo **liveable**.

### Tooling: [`buf`](https://buf.build)

```yaml
# proto/buf.yaml
version: v2
lint:
  use: [STANDARD]
breaking:
  use: [FILE]
```

```yaml
# proto/buf.gen.yaml
version: v2
plugins:
  - remote: buf.build/protocolbuffers/csharp
    out: ../building-blocks/gen/csharp
  - remote: buf.build/grpc/csharp
    out: ../building-blocks/gen/csharp
  - remote: buf.build/protocolbuffers/go
    out: ../building-blocks/gen/go
  - remote: buf.build/grpc/go
    out: ../building-blocks/gen/go
  - remote: buf.build/protocolbuffers/python
    out: ../building-blocks/gen/python
  - remote: buf.build/protocolbuffers/java
    out: ../building-blocks/gen/java
```

### Non-negotiable rules

1. **Nobody edits files under `building-blocks/gen/`.** Add to `.gitattributes`: `building-blocks/gen/** linguist-generated=true`
2. **`buf breaking` runs in CI** on every PR, against `main`. A broken contract is a red PR.
3. **Version in the path** (`order/v1/`). Need a breaking change? Create `v2`, run both, deprecate gradually.
4. **`proto/rpc/` and `proto/events/` are different in kind:**
   - `rpc/` = *"I'm asking you for this"* → verb names (`GetOrder`, `ReserveStock`)
   - `events/` = *"this already happened"* → past-tense names (`OrderPlaced`, `StockReserved`)
5. Generate with `make proto` and **commit the output**. Reason: a fresh clone builds immediately, without installing `buf`.

---

## 5. Communication rule: gRPC or Kafka

This is the decision most often got wrong in microservices. The one-line rule:

> **Need an answer before you can continue → gRPC. Only announcing that something happened → Kafka.**

| Situation | Choice | Why |
|---|---|---|
| Gateway → any service | **gRPC** | A client is waiting on the response |
| Order → Inventory: reserve stock | **gRPC** | You can't create the order without knowing stock is available |
| Order → Catalog: item details at checkout | **local read model** | You can't price the order without them — but see [section 18](#18-order-items-and-master-data--copy-or-look-up), the result is **copied**, never re-read later |
| Order → Payment: charge | **Kafka** (saga) | May take seconds; must not block the user |
| Order → Search: reindex | **Kafka** | Search being two seconds stale is fine |
| Order → Recommendation: record behaviour | **Kafka** | Fire-and-forget |
| Clickstream → everywhere | **Kafka** | High volume, many consumers |
| Everything → Stream processor | **Kafka** | That is what streaming is |

### Diagram

```
                    ┌──────────────┐
   Browser ────────▶│ api-gateway  │ [C# + YARP]  BFF, auth, rate limiting
                    └──────┬───────┘
          ┌────────────────┼────────────────┬──────────────────┐
          │ gRPC           │ gRPC           │ gRPC             │ gRPC
          ▼                ▼                ▼                  ▼
   ┌─────────────┐  ┌─────────────┐  ┌──────────────┐  ┌──────────────────┐
   │   order     │  │   search    │  │  inventory   │  │ recommendation   │
   │    [C#]     │  │    [Go]     │  │    [Go]      │  │    [Python]      │
   │  Postgres   │  │ OpenSearch  │  │   Postgres   │  │    pgvector      │
   └──────┬──────┘  └──────▲──────┘  └──────┬───────┘  └────────▲─────────┘
          │ outbox         │ index          │                   │ features
          ▼                │                ▼                   │
   ╔══════════════════════════════════════════════════════════════════════╗
   ║           KAFKA  ·  ordering.events · catalog.events · clickstream   ║
   ╚═══════════════════════════════╤══════════════════════════════════════╝
                                   │
                        ┌──────────▼───────────┐
                        │  stream-processor    │ [Java / Flink]
                        │  window · state · CEP│
                        └──────────┬───────────┘
                                   ▼
                        ClickHouse  ·  Iceberg/MinIO
```

---

## 6. Local development experience

The goal: **`git clone` → `make up` → it runs.** There is no third step.

### The Makefile is the repo's API

```makefile
.PHONY: up infra down proto proto-check test arch lint

up:            ## Start the whole system
	docker compose -f compose.infra.yml -f compose.services.yml up -d --build

infra:         ## Infrastructure only — for debugging a single service from the IDE
	docker compose -f compose.infra.yml up -d

down:
	docker compose -f compose.infra.yml -f compose.services.yml down -v

proto:         ## Regenerate every stub from proto/
	cd proto && buf lint && buf generate

proto-check:   ## Detect breaking changes against main
	cd proto && buf breaking --against '.git#branch=main'

test:          ## Run tests across all four languages
	dotnet test services/order-service
	dotnet test services/api-gateway
	cd services/catalog-service && go test ./...
	cd services/inventory-service && go test ./...
	cd services/recommendation-service && pytest
	cd services/stream-processor && mvn test

arch:          ## Architecture tests — enforce service and layer boundaries
	dotnet test tests/arch/dotnet
	cd tests/arch/go && go-arch-lint check
	cd tests/arch/python && lint-imports

lint:          ## Lint the whole repo
	cd proto && buf lint
	cd services/catalog-service && golangci-lint run
	cd services/inventory-service && golangci-lint run
	cd services/recommendation-service && ruff check .
```

### Split compose into two files

| File | Contents | Lifecycle |
|---|---|---|
| `compose.infra.yml` | Kafka, Schema Registry, Postgres, Redis, OpenSearch, MinIO, OTel Collector, Grafana | Started once in the morning, left alone all day |
| `compose.services.yml` | Your 7 services (3 C#, 2 Go, 1 Python, 1 Java) | Restarted constantly while coding |

> Keep them in one file and every Go edit means waiting for Kafka to boot again. By week two you will resent your own project.

### Non-negotiable

- [ ] **Healthchecks** on every container + `depends_on: condition: service_healthy`
- [ ] A seed-data script — with no data there is nothing to demo
- [ ] A complete `.env.example`; `make up` works with zero edits

---

## 7. Cross-language observability

**This is the highest-visibility work for the least effort in the whole project.** A single trace running **C# gateway → Go → C# → Kafka → Python** and rendering as **one unbroken line** in Grafana Tempo is more persuasive than any code sample.

| Concern | Approach |
|---|---|
| Instrumentation | OpenTelemetry SDK for .NET, Go, Python and Java — **never a language-specific tracing library** |
| Propagation | W3C `traceparent` header for gRPC; **Kafka message headers** for async |
| Collector | One OTel Collector in `compose.infra.yml`; every service exports to it |
| Backend | Tempo (traces) + Prometheus (metrics) + Loki (logs) + Grafana |
| Correlation | Every log line **must** carry `trace_id` — that's the join key between logs and traces |
| **Trace origin** | **`api-gateway` mints the first `traceparent`.** Every other span descends from the gateway span. Since the gateway is .NET, you use `ActivitySource` + `AddAspNetCoreInstrumentation()` — the same mechanism as order-service, not a second SDK to learn. |

> **The hard part — and the part worth showing off:** trace context does not cross Kafka automatically. You inject it into headers in the producer and extract it in the consumer, in **all four languages**. Once it works, write `docs/09-architecture-decisions/00X-distributed-tracing.md`.

---

## 8. Six-month roadmap

### Month 0.5 — Walking skeleton *(2 weeks)*

> **Goal:** one request travelling browser → gateway → one service → DB, with a visible trace.

- [ ] Repo skeleton per section 2
- [ ] **`mise.toml`** pinning .NET / Go / Python / Java / buf — *day one, not later*
- [ ] Scaffold `docs/` on the arc42 skeleton (12 empty files with headings — fill in over time)
- [ ] **`.agents/skills/` + `CLAUDE.md` + `AGENTS.md`** — see [section 15](#15-agent-skills--pinning-ai-guidance-into-the-repo).
      Write `proto-contract` and `ddd-dotnet` in week one; add the rest as each language appears.
- [ ] `proto/rpc/order/v1/order_service.proto` — a single `GetOrder` RPC
- [ ] `make proto` generates stubs for C# and Go
- [ ] `order-service` (C#) returning hard-coded data
- [ ] `api-gateway` (C# + YARP) exposing REST `/api/orders/{id}` → gRPC client call to order-service
- [ ] `compose.infra.yml` + `compose.services.yml` + `Makefile`
- [ ] OTel Collector + Grafana Tempo — **a visible two-hop trace**
- [ ] CI: build both services + `buf lint`

**Done when:** `make up && curl localhost:8080/api/orders/1` returns a result, and the trace shows both spans.

---

### Month 1 — Core domain (C#)

- [ ] **order-service**, complete:
  - [ ] `Domain/` — `Order` aggregate, `OrderItem`, `Address` value object, domain events
  - [ ] **`OrderItem` snapshots the item** — `product_id`, `product_name`, `sku`, `unit_price`,
        `currency`, `tax_rate`, `quantity` — see [section 18](#18-order-items-and-master-data--copy-or-look-up).
        Get this shape right now and nothing downstream needs rewriting.
  - [ ] **Immutability test**: change the seeded product, re-read an existing order, assert nothing
        moved. This is what stops someone normalising it away in month 4.
  - [ ] `Application/` — MediatR + `Logging → Validation → Transaction` pipeline
  - [ ] `Infrastructure/` — EF Core, Postgres, migrations
  - [ ] Idempotency: `IdentifiedCommand` + `RequestManager`
- [ ] **payment-service** (C#) — minimal, just enough to act as a saga participant
- [ ] Domain unit tests **with no DB mocking**
- [ ] Integration tests via **Testcontainers**

#### ★ Backend depth — concurrency and schema change

> Two questions asked in essentially every backend interview, neither of which "having services" answers.

- [ ] **Optimistic concurrency** on the `Order` aggregate — a row version column, and a deliberate
      `DbUpdateConcurrencyException` path. Two users editing one order must not silently overwrite
      each other.
      → be able to explain optimistic vs pessimistic, and why an aggregate suits optimistic
- [ ] **Expand–contract migration** — practise a zero-downtime column rename:
      add new column → backfill → write both → switch reads → stop writing old → drop.
      Do it once deliberately, so *"how do you change a schema without downtime?"* has a real answer.
- [ ] **Migration ordering rule** — code that tolerates both old and new schema deploys first;
      the destructive migration ships in a later release, never the same one

#### ★ Architecture tests — start in month 1, not at the end

> This is what turns *"I follow DDD"* into *"violating DDD turns the build red"*. In a polyglot repo it matters even more — the boundaries between four languages are the easiest to break and the hardest to notice.

| Language | Tool | Rules to enforce from month 1 |
|---|---|---|
| C# | `NetArchTest` / `ArchUnitNET` | `Domain/` must not reference `Infrastructure/` or `building-blocks/`<br>Aggregates must inherit `Entity`<br>Domain events must be immutable `record`s<br>Command handlers must live in `Application/` |
| Go | `go-arch-lint` | `internal/handler` must not import `internal/repository` directly<br>No package may import `gen/csharp` |
| Python | `import-linter` | `app/models` must not import `app/consumers` |
| **All languages** | hand-written tests | **No service may import another language's `gen/`**<br>**No service may reference another service's packages** |

- [ ] `tests/arch/dotnet` — at least 8 rules, running in CI
- [ ] `tests/arch/go` and `tests/arch/python` — added as those services appear (months 2 and 3)
- [ ] The single most valuable rule: a **cyclic dependency test** — catches a distributed monolith early

---

### Month 2 — Go services + event backbone ★

> The month the project stops being "some APIs" and becomes an event-driven system.

- [ ] **catalog-service** (Go) — owns products, serves search
  - [ ] gRPC handlers + OpenSearch client
  - [ ] Kafka consumer → index updates
  - [ ] Measure p99 latency; target < 50 ms
- [ ] **inventory-service** (Go) — reserve/release stock, handle contention
- [ ] **`order-service` local product read model** — so checkout survives catalog being down
      ([section 18](#18-order-items-and-master-data--copy-or-look-up))
- [ ] **Price validation at checkout** + `docs/09-architecture-decisions/00X-price-at-checkout.md`
      recording which policy was chosen and why
- [ ] **Outbox pattern** in order-service
- [ ] **Debezium** reading the Postgres WAL → Kafka *(a plain publisher is acceptable as a first step)*
- [ ] `proto/events/` + Protobuf serialization through Schema Registry
- [ ] **Partition key design** → `docs/08-cross-cutting-concepts/partition-strategy.md`
- [ ] DLQ + retry topic with exponential backoff
- [ ] `building-blocks/chassis-go/` — shared otel middleware and Kafka wrapper

#### ★ Backend depth — resilience and caching

> The chassis lists `Resilience/` and `Caching/` as folders. This is where they stop being folder names.

- [ ] **Resilience on every gateway → service call** (Polly): timeout, retry with **jittered** backoff,
      circuit breaker. Retry without jitter creates a thundering herd — know why.
- [ ] **Prove it works** — make `inventory-service` sleep 5 s, watch the breaker open, confirm the
      gateway degrades instead of hanging
- [ ] **Cache-aside in `catalog-service`** — Redis, TTL, and **invalidation driven by a Kafka event**
      rather than by guesswork
- [ ] **Cache stampede protection** — single-flight, so one expired key doesn't send 500 concurrent
      requests to OpenSearch
- [ ] **Connection pool sizing** — document the pool size per service and why. The classic failure is
      200 worker threads sharing a pool of 10.

**Done when:** killing order-service mid-publish and restarting loses nothing and duplicates nothing —
and a slow downstream service trips the breaker instead of taking the gateway down with it.

---

### Month 3 — Saga & the Python service

- [ ] **Order saga** over Kafka:

  ```
  Created → StockReserved → Paid → Confirmed
      │           │            │
      └─ StockRejected ────────┴─ PaymentFailed ──→ Cancelled
  ```

- [ ] Choose **orchestration** (a state machine inside order-service) over choreography
      → `docs/09-architecture-decisions/00X-saga-orchestration.md`
- [ ] Stuck-saga detection + timeouts
- [ ] **recommendation-service** (Python)
  - [ ] gRPC server (grpcio)
  - [ ] Product embeddings via `sentence-transformers` → pgvector
  - [ ] `GetSimilarProducts` + `GetRecommendationsForUser`
  - [ ] Kafka consumer updating user behaviour features
- [ ] **E2E test** through the gateway: place an order → assert the final state

#### ★ Backend depth — authentication and authorization

> Until now the plan says "JWT" and stops. That is not an answer to any interview question.
> This is the single largest backend gap in the original plan.

- [ ] **Identity provider**: Keycloak in `compose.infra.yml` (or Duende IdentityServer if you prefer
      staying inside .NET)
- [ ] **Authorization Code flow with PKCE** for the browser → gateway
- [ ] **Service-to-service auth** — client credentials, or token exchange when a service acts on a
      user's behalf. Be able to explain the difference and when each applies.
- [ ] **Policy-based authorization**, not role strings scattered through controllers
- [ ] **Resource ownership check** — user A must not be able to read user B's order. Write the test
      that proves it.
- [ ] **Token propagation through the saga** — when an async handler acts later, whose authority is
      it acting under? Answer this explicitly; it is a genuinely hard question and a strong one to
      have thought about.

---

### Month 4 — The Flink layer (Java) ★

> The month that decides this portfolio's value for a Data Engineer role.

Three jobs, each teaching a different concept:

| # | Job | Concept |
|---|---|---|
| 1 | `revenue-rollup` | Tumbling windows, watermarks, event time |
| 2 | `fraud-detection` | Keyed state, timers, CEP |
| 3 | `sessionization` | Session windows, late data, side outputs |

You must be able to do **and explain**:

- [ ] Watermark strategy — what bounded out-of-orderness you chose, and why
- [ ] Checkpointing — interval, aligned vs unaligned, RocksDB state backend
- [ ] **Exactly-once** — Kafka source + transactional sink (two-phase commit)
      → *prove it:* kill a TaskManager mid-run, the count is unchanged
- [ ] **Savepoints & rescaling** — stop the job, raise parallelism, restore
- [ ] At least one job written in **Flink SQL**

> Every job ships with a `README.md` explaining its design decisions. This is your interview script.

---

### Month 4.5 — Batch pipeline & master data ★ *(2–3 weeks)*

> Full design in [section 16](#16-batch-pipeline--master-data-ingestion). This is the phase that turns a
> streaming project into a **data platform** — and it is what most Data Engineer job descriptions
> actually ask for, since almost none of them are streaming-only.

- [ ] **Airflow** in `compose.infra.yml`, DAGs in `data-platform/orchestration/dags/`
- [ ] **Source 1 — supplier catalogue**: daily full snapshot (Parquet on MinIO, standing in for an SFTP drop)
- [ ] **Source 2 — partner pricing**: paginated REST pull, incremental by high-water mark on `updated_at`
- [ ] **Bronze → silver → gold** in dbt; **SCD Type 2** for products in silver
- [ ] **Data quality gate** between silver and gold — a bad batch must never reach gold
- [ ] **Publisher**: gold → **log-compacted Kafka topic** `catalog.product.master.v1`
- [ ] **catalog/search services consume it** — the batch pipeline never writes to a service database
- [ ] **Switch `order-service`'s product read model** to this topic — the snapshot logic is untouched,
      only the source changes ([section 18](#18-order-items-and-master-data--copy-or-look-up))
- [ ] **Re-run the month-1 immutability test** — it must still pass. That regression is the proof the
      staged migration was safe.
- [ ] ★ **Enrichment join in Flink** — `revenue-rollup` gains revenue by category, brand and supplier
- [ ] **Backfill test** — replay 90 days through the same DAG; totals must be identical, not doubled
- [ ] **Failure test** — kill a task mid-run, re-run the same logical date, verify no duplicates

**Done when:** a product renamed in the supplier feed at 02:00 appears in the Flink revenue breakdown
by 02:30, without any service reading another service's database.

> ⏱ **Timeline honesty:** this adds 2–3 weeks to a plan with little slack. Month 6 packaging is what
> will compress. If something has to give, drop the `sessionization` Flink job or the Iceberg cold
> layer — **not** this phase and not the packaging.

---

### Month 5 — Serving layer & completion

- [ ] **ClickHouse** — written to by Flink, serving the dashboard
- [ ] **Iceberg on MinIO** — the lakehouse layer; compare hot vs cold queries
- [ ] Real-time dashboard (Grafana or a small web app)
- [ ] **Complete end-to-end trace**: browser → C# → Go → Kafka → Java → ClickHouse
- [ ] Metrics: consumer lag, checkpoint duration, backpressure, per-service gRPC p99
- [ ] Data quality: null checks, schema drift, late-arrival rate
- [ ] Dashboard splits revenue by **category / brand / supplier** — only possible because of month 4.5
- [ ] Freshness metric per pipeline: *how old is the newest master data the stream is joining against?*

#### ★ Backend depth — behaviour under load

- [ ] **Rate limiting at the gateway**, per user and per client — and a deliberate decision about what
      a caller sees when it trips (`429` + `Retry-After`, not a generic `500`)
- [ ] **Load shedding** — the gateway sheds work and returns `503` before it collapses. Explain the
      difference between rate limiting (policy) and load shedding (self-preservation).
- [ ] **Graceful shutdown** — drain in-flight requests, stop consuming Kafka, commit offsets, then exit.
      A pod killed mid-request must not lose it.
- [ ] **Health checks that mean something** — liveness ≠ readiness. Readiness fails while a dependency
      is down; liveness only fails when the process itself is broken. Getting this backwards causes
      restart loops.

---

### Month 6 — Hardening & presentation

- [ ] **Chaos testing** → `docs/failure-modes.md`
  - kill a broker · kill a TaskManager · network partition · a service dying mid-saga
- [ ] **Load testing** — k6 through the gateway, 10k rps, p99 measured per hop
- [ ] Backfill/replay — reset a consumer group, replay 30 days, reconcile the results
- [ ] Deploy to **kind** (local K8s) — Helm chart or kustomize

#### ★ Backend depth — releasing safely

- [ ] **Zero-downtime rolling update** — readiness gates, `maxUnavailable`, PodDisruptionBudget
- [ ] **Blue-green for the gateway** — it is the single front door, so it is the one component where
      an instant rollback is worth the extra machinery
- [ ] **Deploy ordering under a contract change** — when `proto/` changes, which side ships first?
      (Consumer-tolerant first, producer second.) Write this down; it's a common interview question
      and most candidates have never thought about it.
- [ ] **Secrets** — not in `.env`, not in git. Even locally, use Docker secrets or SOPS so the habit
      is right.
- [ ] **A rollback drill** — deploy a deliberately broken version, roll back, and time it
- [ ] **12–15 ADRs** in `docs/09-architecture-decisions/`
- [ ] `docs/runbook.md` — "service X is down, now what"
- [ ] Complete all 12 arc42 chapters — especially **10-quality-requirements** and **11-risks-and-technical-debt**

#### ★ Packaging — two days of work that decide whether the repo gets read at all

> A visitor decides in **30 seconds** whether to open your code. Six months of work hangs on those 30 seconds.

- [ ] **An architecture diagram as an image** in the README — not ASCII art; PNG/SVG from Excalidraw or Structurizr
- [ ] **Screenshots** of the real-time dashboard, the four-language Grafana trace, and the Flink UI
- [ ] **A demo GIF** — `make up` → place an order → watch the numbers move on the dashboard
- [ ] **Badges**: CI status, coverage, license, .NET/Go/Python versions
- [ ] **GitHub Pages** publishing `docs/` (MkDocs Material is enough and faster than Docusaurus)
- [ ] A tightened README: goal → diagram → service/language table → "run it in two commands" → link to the docs site
- [ ] **Security CI**: CodeQL + Trivy + Dependabot *(nearly copy-paste YAML, strong signal for platform roles)*
- [ ] Re-verify `git clone && mise install && make up` on a **clean machine**

---

## 9. CI/CD for a polyglot monorepo

The core problem: **don't rebuild everything when one service changes.**

```yaml
# .github/workflows/ci.yml  (abridged)
jobs:
  changes:
    outputs:
      order:   ${{ steps.filter.outputs.order }}
      gateway: ${{ steps.filter.outputs.gateway }}
      catalog: ${{ steps.filter.outputs.catalog }}
      proto:   ${{ steps.filter.outputs.proto }}
    steps:
      - uses: dorny/paths-filter@v3
        id: filter
        with:
          filters: |
            # The gateway is C# now, so it depends on gen/csharp AND chassis-dotnet,
            # exactly like order-service. A proto change rebuilds both.
            order:   ['services/order-service/**',  'building-blocks/gen/csharp/**', 'building-blocks/chassis-dotnet/**']
            gateway: ['services/api-gateway/**',    'building-blocks/gen/csharp/**', 'building-blocks/chassis-dotnet/**']
            catalog: ['services/catalog-service/**', 'building-blocks/gen/go/**',     'building-blocks/chassis-go/**']
            proto:   ['proto/**']

  proto-check:
    if: needs.changes.outputs.proto == 'true'
    steps:
      - run: buf lint
      - run: buf breaking --against '.git#branch=main'   # ← blocks breaking changes

  order-service:
    if: needs.changes.outputs.order == 'true'
    steps: [dotnet build, dotnet test]

  api-gateway:
    if: needs.changes.outputs.gateway == 'true'
    steps: [dotnet build, dotnet test]

  # ... and so on per service
```

> **Consequence of moving the gateway to C#:** a `proto/` change used to rebuild one .NET project; now it rebuilds **two** (order and gateway), since both consume `building-blocks/gen/csharp/`.
> Slightly slower CI, but in exchange **a breaking change fails at both ends of the contract at once** — client (gateway) and server (order) go red together, rather than surfacing later at runtime.

**Three jobs you cannot skip:**

| Job | What it blocks |
|---|---|
| `buf breaking` | A proto change that breaks a running consumer |
| `gen-is-current` | Editing `.proto` but forgetting `make proto` — CI regenerates and diffs; any difference is red |
| `e2e` | Every service passing individually while the assembled system is broken |

---

## 10. Known pitfalls

| # | Pitfall | How to avoid it |
|---|---|---|
| 1 | **Splitting services too early** | Seven services in week one is too many. Start with **two** (order + gateway) — both are C# now, so month 0.5 needs only one toolchain. Add Go in month 2, Python in month 3, Java in month 4. |
| 2 | **A shared database** | Each service owns its own schema, and **no service queries another service's tables**. Break this and you have a distributed monolith. |
| 3 | **`building-blocks` bloat** | Infrastructure code only (otel, kafka, logging). **Never** domain logic — if two services need the same domain logic, the service boundary is wrong. |
| 4 | **Four different logging styles** | Standardise on **structured JSON** with identical field names (`trace_id`, `service`, `level`) from week one. Retrofitting is miserable. |
| 5 | **Skipping proto versioning** | Put `v1/` in the path from the start, even when you don't need it yet. |
| 6 | **A slow Python service** | Python gRPC really is a bottleneck. Use `grpcio` with a large enough thread pool, consider `uvloop`. Measure before worrying. |
| 7 | **Nobody else can run the repo** | Re-test `git clone && mise install && make up` on a clean machine once a month. |

---

## 11. Out of scope

| Not building | Why |
|---|---|
| Real payments (Stripe) | Simulation is enough; payment integration teaches nothing about distributed systems |
| Admin CMS, user management, i18n | Time-consuming, demonstrates nothing |
| A mobile app | Irrelevant to the goal |
| Service mesh (Istio/Linkerd) | Large complexity, low learning value for the effort |
| A sophisticated recommendation model | Embeddings + cosine similarity is already impressive enough |
| Hand-rolled service discovery | Docker DNS / K8s Services suffice |
| A full notification service (email/SMS templates, provider integration) | No interview asks about SMTP. The WebSocket push the dashboard needs belongs in `api-gateway` (SignalR, ~2 days); the idempotent-side-effect lesson is already 80% covered by the existing consumers. |
| **`recommendation-service`** — *reconsidered, see [section 17](#17-dual-track-plan--backend-and-data-engineer)* | Weak for both tracks, and month 4.5 already earns Python's place in the repo. Cutting it funds the backend depth blocks. |

---

## 12. Interview map

### Data Engineer (Streaming)

| Question | Answered by |
|---|---|
| "How does exactly-once work in Flink?" | Job 1 + the TaskManager-kill chaos test |
| "How do you choose a partition key?" | `docs/08-cross-cutting-concepts/partition-strategy.md` |
| "How do you handle late data?" | Job 3 — session windows + side outputs |
| "What happens when a schema changes?" | `buf breaking` in CI + Schema Registry |
| "Consumer lag is climbing — what do you do?" | The Grafana dashboard + rescaling from a savepoint |
| "How is CDC different from outbox polling?" | Month 2 — both were built and measured |
| "What is backpressure and how do you detect it?" | Flink UI + the metrics already wired up |
| "Stateful vs stateless processing?" | Jobs 2 and 3 |
| "How do you join a stream against slowly-changing data?" | Month 4.5 — the enrichment join, section 16 |
| "Full load or incremental — how do you choose?" | Month 4.5 — two sources, one of each |
| "What is log compaction and when do you use it?" | Section 16 — master data topics |
| "How do you model dimension history?" | SCD Type 2 in the silver layer |
| "A nightly job fails at 3am — what happens?" | Idempotent tasks + high-water marks, section 16 |
| "How do you backfill 90 days without double counting?" | Month 4.5 — same DAG, date range, idempotent by design |
| "What if a saga gets stuck?" | Month 3 — orchestration + stuck-saga detection |

### Backend / Platform

| Question | Answered by |
|---|---|
| "How do you draw service boundaries?" | `docs/08-cross-cutting-concepts/service-boundaries.md` |
| "Why use several languages?" | `docs/09-architecture-decisions/001-why-polyglot.md` — trade-offs admitted |
| "gRPC, REST, or a message queue?" | Section 5 — an explicit rule, not a case-by-case guess |
| "How do you handle distributed transactions?" | The month 3 saga orchestration |
| "How do you debug a failure across four services?" | The month 5 distributed tracing |
| "How do you deploy services independently?" | CI path filters + versioned proto |
| "How do you change a database schema with no downtime?" | Month 1 — expand–contract, practised once deliberately |
| "Optimistic or pessimistic locking — which and why?" | Month 1 — row version on the Order aggregate |
| "A downstream service gets slow. What happens?" | Month 2 — circuit breaker, proven with a 5 s sleep |
| "Why does retry need jitter?" | Month 2 — thundering herd |
| "How do you invalidate a cache?" | Month 2 — Kafka-event-driven invalidation, plus stampede protection |
| "Walk me through your auth flow." | Month 3 — Keycloak, Authorization Code + PKCE |
| "How do services authenticate to each other?" | Month 3 — client credentials vs token exchange |
| "An async handler runs later — whose authority is it acting under?" | Month 3 — token propagation through the saga |
| "Rate limiting vs load shedding?" | Month 5 — both, with different response codes |
| "Liveness vs readiness probes?" | Month 5 — and what breaks when you swap them |
| "Proto changed. Which side do you deploy first?" | Month 6 — consumer-tolerant first |
| "How long does a rollback take?" | Month 6 — measured in a drill, not guessed |

---

## 13. Progress tracker

| Phase | Key deliverable | Status |
|---|---|---|
| M0.5 — Skeleton | `make up` + a two-hop trace | ☐ |
| M1 — Core domain | Order aggregate + Testcontainers tests | ☐ |
| M2 — Go + events ★ | Outbox → Kafka, nothing lost or duplicated | ☐ |
| M3 — Saga + Python | A successful end-to-end order through the gateway | ☐ |
| M4 — Flink ★ | Three jobs + a proof of exactly-once | ☐ |
| M4.5 — Batch pipeline ★ | Master data reaching Flink via a compacted topic | ☐ |
| M5 — Serving | An unbroken trace across four languages | ☐ |
| M6 — Hardening | `failure-modes.md` + 15 ADRs | ☐ |
| M6 — Packaging | Docs site + screenshots + badges + diagram | ☐ |

---

## 14. Comparison with BookWorm

[`foxminchan/BookWorm`](https://github.com/foxminchan/BookWorm) is a well-regarded .NET/Aspire reference repo (⭐ 504). This section records **why this project deliberately diverges** — so that three months from now the reasoning is still on record.

### BookWorm's actual scale

| | |
|---|---|
| Timeline | July 2024 → present, **over two years** |
| Commits | **597** — by exactly **one person**; the other seven "contributors" are bots |
| Size | 3,377 files · 10 services · 2 Next.js apps |
| Languages | **~99% C#** |

> ⚠️ Do not measure your progress against this repo. You have six months; he has had two years. Compare **how it was built**, not **how much**.

### What to take — already folded into this plan

| Practice | Where it now lives |
|---|---|
| `mise.toml` pinning toolchain versions | Section 2 + month 0.5 |
| Documentation on the **arc42** 12-chapter template | Section 2 (`docs/`) + months 0.5 and 6 |
| **Architecture tests** enforcing boundaries | Month 1 (extended to Go and Python in months 2–3) |
| A **chassis** with explicit, named modules | Section 2 (`building-blocks/chassis-*`) |
| **Contract tests living inside each service** | Section 2 — not in an external `tests/contract/` |
| **Packaging**: docs site, screenshots, badges, image diagrams | Month 6 |
| Security CI: CodeQL, Trivy, Dependabot | Month 6 |
| **Agent skills pinned into the repo** (`.agents/skills/`) | Section 15 + month 0.5 |

### What to avoid — and why

| He does | We don't | Reason |
|---|---|---|
| **10 services** | 7, and we hold section 11 firmly | Two years vs six months. Another service is more surface to maintain, not more credit. |
| **Every pattern at once** — event sourcing + inbox + feature flags + API versioning + CQRS + VSA + saga | Only patterns with a reason in *this* context, each with an ADR | A sharp interviewer will ask *"why event sourcing here?"*. *"To show I can"* is a weak answer. |
| **An AI/agent layer**: MCP, A2A, AG-UI, multi-agent | Cut entirely | Fashionable, but orthogonal to Data Engineer (Streaming). |
| **Two Next.js apps + WCAG 2.1 AA** | One simple dashboard | Roughly two months of work to demonstrate a skill nobody will ask you about. |
| **Vertical Slice Architecture** | Keep layered `Domain/Application/Infrastructure` | VSA suits CRUD-heavy domains. Ours has saga orchestration and non-trivial aggregate invariants, where layering reads more clearly. *This is a choice, not a law — write the ADR.* |

### What BookWorm cannot teach you — and where you differentiate

BookWorm uses Kafka **as a message queue**. It has none of:

- ❌ Deliberate partition strategy · ❌ Consumer lag monitoring · ❌ Replay from offset
- ❌ Schema Registry / schema evolution · ❌ CDC / Debezium
- ❌ Stream processing (Flink, windowing, watermarks, keyed state) · ❌ Exactly-once semantics
- ❌ A serving layer or lakehouse

**Months 2 and 4 of this plan are blank space in his repo.** For a Data Engineer (Streaming) role, BookWorm is nearly useless as a *technical* template — but it is an excellent template for *how to build and present* a project.

> **The takeaway: learn *how* he built it, not *what* he built.**

---

## 15. Agent skills — pinning AI guidance into the repo

> **Why this matters more here than in BookWorm:** his repo is ~99% C#, one set of conventions. Yours has **four languages and four sets of conventions**. Without guidance pinned into the repo, an AI agent will write Go as if it were C#, write Flink jobs with batch thinking, and invent a new Kafka topic naming scheme every time.

### What an agent skill is

A directory containing a `SKILL.md` — YAML frontmatter plus instructions — that AI coding agents (Claude Code, Copilot, Cursor) load automatically when a relevant task appears. It is **not runtime code**, and has nothing to do with AI product features (which section 11 puts out of scope).

```
.agents/skills/kafka-conventions/
├── SKILL.md              # short — loaded on trigger
└── references/           # long — loaded only when actually needed
    ├── topic-naming.md
    └── partition-keys.md
```

**Two techniques decide whether a skill is usable:**

| Technique | Why it's needed |
|---|---|
| `USE FOR` / `DO NOT USE FOR` in the `description` | The agent reads the description to **choose** a skill. With seven skills and no explicit "when not to use this", it will keep picking the wrong one. |
| **Progressive disclosure** — short `SKILL.md`, `references/` loaded on demand | Cramming everything into `SKILL.md` burns context on every trigger, even when only a tenth of it is relevant. |

### Skills to **author** — in the order you'll need them

| # | Skill | Write it | Must contain |
|---|---|---|---|
| 1 | **`proto-contract`** | Week 1 | The workflow for changing `.proto`: `buf lint` → `buf breaking` → `make proto` → commit `gen/`. **Never hand-edit anything under `gen/`.** Version in the path (`v1/`). `rpc/` takes verb names; `events/` takes past-tense names. |
| 2 | **`ddd-dotnet`** | Week 1 | `Domain/` must not reference `Infrastructure/` or `building-blocks/`. Aggregates inherit `Entity`. Domain events are immutable `record`s. Command handlers live in `Application/`. Cross-check against `tests/arch/` — the skill and the architecture test must state **the same rule**. |
| 3 | **`kafka-conventions`** | Month 2 | Topic naming, partition key selection, mandatory DLQ + retry topic, trace context in headers, commit offsets **after** processing, idempotent consumers. |
| 4 | **`go-service`** | Month 2 | `cmd/server/main.go`, no root `main.go`. `internal/handler` must not import `internal/repository` directly. Error wrapping, context propagation, structured logs using the same field names as .NET. |
| 5 | **`flink-job`** | Month 4 | Every job **must** declare a watermark strategy and justify the number. Explicit checkpoint configuration. Late data: side output or drop — a conscious choice. Every job ships a `README.md` explaining its design. |
| 6 | **`arc42-docs`** | Month 1 | Which chapter new documentation belongs to. ADRs go to `09-`, topic papers to `08-`. ADR format: Context / Decision / Consequences / Alternatives considered. |

### Skills to **vendor** rather than write

| Skill | Source | Use when |
|---|---|---|
| `aspire`, `aspireify`, `aspire-orchestration` | Microsoft | If you use Aspire for the .NET tier |
| `csharp-tunit` or `csharp-xunit` | community | C# test conventions |
| `vercel-react-best-practices` | Vercel | **Only if** the dashboard is Next.js — skip it for Blazor |
| `catalog-documentation-creator` | EventCatalog | If you later publish the event catalog as a site |

> **Vendoring means copying into the repo, not installing globally.** The point is that **everyone — and every machine — gets an AI that behaves the same way**, including you three months from now.

### `SKILL.md` template — usable as-is for `kafka-conventions`

```markdown
---
name: kafka-conventions
description: >-
  Mandatory rules for working with Kafka in this repo — topic naming,
  partition key selection, DLQ, idempotent consumers, trace context.
  USE FOR: creating a topic, writing a producer/consumer, adding an integration
  event, editing files under proto/events/, configuring a consumer group, retries.
  DO NOT USE FOR: Flink jobs (use flink-job), proto schema changes
  (use proto-contract), gRPC request/response (use proto-contract).
metadata:
  version: "1.0"
---

# Kafka Conventions

## Topic naming

`<context>.<aggregate>.<v1>` — e.g. `ordering.order.v1`, `catalog.product.v1`.
Singular, lowercase. The version goes at the end, never in the middle.

## Partition keys — hard rules

| Topic | Key | Why |
|---|---|---|
| `ordering.order.v1` | `OrderId` | Every event for one order must stay in order |
| `user.click.v1` | `SessionId` | Sessionization needs one session in one partition |

**Never** key `ordering.*` by `CustomerId` — high-volume customers create hot partitions.

## Required of every consumer

1. Idempotent — reprocessing the same message must not change the outcome
2. Commit offsets **after** processing, never before
3. A DLQ at `<topic>.dlq`, plus a retry topic with exponential backoff
4. Extract trace context from Kafka headers (see `references/tracing.md`)

## Before creating a new topic

Update `docs/08-cross-cutting-concepts/event-catalog.md` **first**, then write code.
No entry in the event catalog means no topic.
```

### Root `CLAUDE.md` — keep it short

Don't repeat skill content. Only what applies to **every** task:

```markdown
# Agent Instructions

## Project language
English only — documentation, code comments, commit messages, ADRs, and
identifiers. No exceptions.

## Standard commands — always use these, never call dotnet/go/pytest directly
- Build & run: `make up` · infrastructure only: `make infra`
- Tests: `make test` · architecture tests: `make arch` · lint: `make lint`
- Regenerate proto stubs: `make proto`

## Absolute rules
1. NEVER hand-edit files under `building-blocks/gen/` — run `make proto`
2. NEVER let one service reference another service's packages
3. NEVER add a Kafka topic without first updating the event catalog
4. Every architectural decision gets an ADR in `docs/09-architecture-decisions/`

## When working in a specific area
Read the matching skill in `.agents/skills/` before writing code.
```

Add a one-line `AGENTS.md` pointing at `CLAUDE.md` so any agent can find it.

### Keeping skills from going stale

| Risk | Countermeasure |
|---|---|
| A skill says one thing while `tests/arch/` enforces another | When a rule changes, **change both in the same PR**. Put it in the PR template checklist. |
| Skills written once and never read | Re-read that month's skills at the end of each month — fifteen minutes |
| Vendored skills drifting from upstream | Record source and version in `metadata`; re-check in month 6 |

---

## 16. Batch pipeline — master data ingestion

> **Assumption stated up front:** "master data from a software source" is read here as reference data
> arriving from **external systems we do not control** — a supplier's product feed, a partner's price
> list, tax and geography tables. If the real source is something else (an ERP database, an internal
> legacy system), the shape of the pipeline is unchanged; only the extractor in
> `data-platform/ingestion/` differs.

### Why a second pipeline rather than forcing it through the first

Streaming carries **facts that happened**: an order was placed, stock moved, someone clicked.
Master data is a different kind of thing entirely: it **describes** the world rather than recording
changes to it. Products, categories, brands, suppliers, price lists, tax rates.

Pushing it down the streaming path is a common and expensive mistake. You would be replaying a
200,000-row supplier catalogue through Kafka every night as though each row were a business event,
paying event-time and watermark costs for data that has no event time.

| | Streaming pipeline | Batch pipeline |
|---|---|---|
| Carries | Facts that happened | Reference data that describes things |
| Trigger | Continuous | Scheduled (nightly, hourly) |
| Source | Our own services | External systems we don't control |
| Volume shape | Many small messages | Few large files |
| Typical failure | Consumer lag climbing | A run that failed and must be re-run |
| Correctness comes from | Exactly-once via checkpoints | Idempotent, re-runnable tasks |
| Tooling | Flink | Airflow + Python + dbt |
| Recovery | Replay from offset | Re-run the same logical date |

Building both — and being able to say clearly when each applies — is worth more in an interview
than doing either one alone. Very few Data Engineer roles are streaming-only.

### Sources — simulated, but realistically shaped

Two sources, deliberately chosen so the pipeline has to handle both loading patterns:

| Source | Shape | Pattern | Teaches |
|---|---|---|---|
| `supplier_catalogue` | Daily Parquet/CSV snapshot dropped in object storage (MinIO standing in for SFTP) | **Full snapshot** | Detecting change by comparison; SCD Type 2; handling deletes |
| `partner_pricing` | Paginated REST API with `updated_since` | **Incremental** | High-water marks, pagination, retries, rate limiting |
| `reference_data` | Small static CSVs (tax rates, regions) | Full reload | Nothing interesting — deliberately trivial, kept for realism |

A full snapshot and an incremental pull have genuinely different failure modes, and being able to
explain when to choose each is a standard interview question.

### Where the data goes — the rule that matters most

> **The batch pipeline never writes into a service's database.**

That rule is not bureaucracy. Break it and `catalog-service` no longer owns its own data, its
architecture tests become a lie, and you have built the distributed monolith that section 10 warns
about.

```
  External source
        │
        ▼
  ┌───────────┐   raw, immutable, partitioned by ingest date — never rewritten
  │  BRONZE   │
  └─────┬─────┘
        ▼
  ┌───────────┐   cleaned, typed, deduplicated, SCD Type 2 history
  │  SILVER   │
  └─────┬─────┘
        │  ◀── DATA QUALITY GATE: a bad batch stops here
        ▼
  ┌───────────┐   serving shapes, one row per product, current state
  │   GOLD    │
  └─────┬─────┘
        ▼
  ╔═══════════════════════════════════════════════════╗
  ║  KAFKA — log-compacted topic                      ║
  ║  catalog.product.master.v1   (key = product_id)   ║
  ╚═══════╤═══════════════════════════════╤═══════════╝
          │                               │
          ▼                               ▼
  catalog-service                  stream-processor
  (owns products + search index)   (broadcast state for
                                    enrichment joins)
```

### Why a log-compacted topic is the bridge

This is the single most important design choice in this section, and worth an ADR.

A compacted topic keeps **the latest value for each key, forever**. That gives three properties at once:

1. **Service ownership survives.** `catalog-service` consumes the topic and decides for itself what to
   store. Nobody reaches into its database.
2. **New consumers bootstrap themselves.** A service added in month 6 reads the topic from the
   beginning and arrives at complete current state — no special backfill path to build.
3. **The two pipelines meet here.** Flink builds broadcast state directly from the same topic. Batch
   output and stream processing connect through one well-defined interface instead of a side channel.

Contrast this with the event topics: `ordering.order.v1` is retention-based, because replaying every
order that ever happened is meaningful. Replaying every historical *version* of a product name is not
— you want the current one. **Different data, different retention policy.** Say exactly this when asked.

### The payoff — the enrichment join

This is why the phase earns its place, rather than being ingestion for its own sake.

Today `revenue-rollup` aggregates order events that carry only product IDs. It can report total
revenue per minute. It cannot report **revenue by category, by brand, or by supplier** — the stream
does not know what a product *is*.

Joining the order stream against master data fixes that, and it is one of the most frequently asked
streaming interview questions.

| Flink approach | When it fits | Note |
|---|---|---|
| **Broadcast state** ★ | Reference data small enough to hold in every task's memory | Recommended here — a product catalogue is tens of thousands of rows |
| Lookup join | Reference data too large to broadcast | Adds an external call per record; needs caching |
| Temporal table join | You need the value **as it was** at event time | The correct choice if you must reprice historical orders accurately |

Choose broadcast state, and write down why in the ADR — including the condition under which you would
switch (catalogue growing past what fits comfortably in task memory).

### Repository placement

```
data-platform/
├── orchestration/dags/     # Airflow DAGs
├── ingestion/              # one extractor per source system
├── transformations/        # dbt models: bronze → silver → gold
├── quality/                # data quality suites
├── publishers/             # gold → compacted Kafka topics
└── tests/
```

**Why this sits outside `services/`:** everything under `services/` runs continuously — it serves
requests or consumes a stream, and it is always up. The batch pipeline is **scheduled work**. Different
lifecycle, different deployment model, different failure mode, different on-call story. Putting it
under `services/` would blur a distinction that is real.

`stream-processor` stays where it is, under `services/`, because a Flink job genuinely does run
continuously.

**Language: Python.** No new toolchain — `recommendation-service` already establishes Python in this
repo, and Airflow and dbt are both Python-native.

### Design rules

| # | Rule | Why |
|---|---|---|
| 1 | **Every task is idempotent** — re-running the same logical date produces the same result | This is what makes a 3am failure survivable. It is the batch equivalent of exactly-once. |
| 2 | **Bronze is immutable** — never updated, never rewritten, only appended by ingest date | When a downstream bug is found six weeks later, you can reprocess from original truth |
| 3 | **High-water marks live in the orchestrator**, not inside the job | A job that remembers its own position cannot be safely re-run or backfilled |
| 4 | **SCD Type 2 in silver** — every product row carries `valid_from` / `valid_to` | Enables "what was this product called when that order was placed" |
| 5 | **The quality gate is between silver and gold** | Bad data must never reach gold, because gold reaches Kafka, and Kafka reaches everything |
| 6 | **Backfill is the same DAG with a date range** — never a separate script | A separate backfill script drifts from the real pipeline and lies to you when you need it most |
| 7 | **Publishing is a separate task from transforming** | So you can rebuild gold without re-emitting to Kafka, and re-emit without rebuilding |

### What you must be able to explain

Each of these is a real interview question that this phase answers:

- Full load versus incremental — how you choose, and what breaks with each
- SCD Type 2 versus Type 1, and when the history is genuinely needed
- Log compaction versus time-based retention, with a concrete example of each in this repo
- How to join a stream against slowly-changing dimension data, and the three Flink options
- What happens when a nightly run fails at 3am, and what makes recovery safe
- How to backfill 90 days without double-counting
- Where batch and streaming meet in this architecture, and why they meet *there*

### Documentation this phase produces

- `docs/08-cross-cutting-concepts/data-lineage.md` — source → bronze → silver → gold → topic → consumer
- `docs/09-architecture-decisions/00X-batch-vs-streaming.md` — what goes down which pipeline, and why
- `docs/09-architecture-decisions/00X-compacted-master-data-topic.md` — the bridge decision above
- Each DAG carries a docstring naming its source, schedule, and owner

---

## 17. Dual-track plan — backend and data engineer

This project serves **two career tracks with equal weight**. That is a constraint, not a slogan: it
changes what gets built, what gets cut, and what gets protected when time runs out.

### The imbalance this section fixes

An audit of the plan before this section existed:

| Signal | Data Engineer | Backend / Platform |
|---|---|---|
| Interview-map questions | 15 | **6** |
| Authentication / authorization | n/a | **absent entirely** |
| Concurrency control, schema migration | n/a | **absent** |
| Release strategy, rollback | n/a | **absent** |
| Circuit breaker, caching | n/a | named once as a chassis folder; **no phase built them** |

The diagnosis matters more than the numbers: months 1–3 were building backend **breadth** — more
services, more endpoints — while never touching backend **depth**. Breadth is not what backend
interviews probe. Nobody asks *"how many services do you have?"*. They ask what happens when one of
them gets slow.

Each phase now carries a `★ Backend depth` block for exactly this reason.

### Which phase feeds which track

| Phase | Backend value | Data value |
|---|---|---|
| M0.5 — Skeleton | ●●○ contracts, tracing, tooling | ●●○ the pipeline's foundation |
| M1 — Core domain | ●●● DDD, concurrency, migrations | ○○○ |
| M2 — Go + events | ●●● resilience, caching, pools | ●●● outbox, CDC, partitioning |
| M3 — Saga + auth | ●●● distributed transactions, OIDC | ●○○ |
| M4 — Flink | ○○○ | ●●● windowing, state, exactly-once |
| M4.5 — Batch | ○○○ | ●●● orchestration, SCD2, batch↔stream |
| M5 — Serving + load | ●●● rate limiting, shedding, shutdown | ●●● serving layer, freshness |
| M6 — Release + hardening | ●●● zero-downtime, rollback, chaos | ●●○ backfill, replay |

Roughly **3 months of backend-weighted work and 2.5 months of data-weighted work**, with M2, M5 and
M6 paying into both. That is the balance to defend.

### One flagship per track — these are never cut

| Track | Flagship | Why this one |
|---|---|---|
| **Data Engineer** | Flink exactly-once, proven by killing a TaskManager, plus the batch↔stream enrichment join | The two things every streaming interview converges on: correctness guarantees, and joining a stream to slowly-changing data |
| **Backend / Platform** | Saga orchestration surviving a service dying mid-flow, with the circuit breaker and rollback drill | Distributed transactions and failure behaviour — the two things backend interviews converge on |

If a month runs long, everything else is negotiable. These two are not.

### Funding the added backend depth

The `★ Backend depth` blocks add roughly **2–3 weeks**. Month 4.5 already consumed the slack, so this
has to be paid for rather than wished away.

**Recommended cut: `recommendation-service`.**

The reasoning is specific rather than arbitrary:

1. It is the **weakest phase for both tracks**. Embeddings and vector similarity are neither backend
   engineering nor stream processing — they are a third discipline that neither interview asks about.
2. Its original justification was *"this is why Python is in the repo"*. **Month 4.5 removed that
   justification** — the batch pipeline is Python (Airflow, dbt), so Python's place in the polyglot
   story is already earned, and earned more convincingly.
3. Cutting it frees **2–3 weeks**, almost exactly the cost of the backend depth blocks.

What you lose: one row in the service table, and the ability to say "I've worked with embeddings".
What you gain: real auth, real resilience, real release engineering — each of which appears in far
more job descriptions than pgvector does.

> If you'd rather keep it: cut the `sessionization` Flink job instead (M4 keeps two jobs, which is
> still enough to demonstrate windowing and state), or accept a seven-month timeline. What you must
> **not** do is keep everything and let the backend depth blocks quietly not happen — that is how the
> plan drifts back to being data-only without anyone deciding it should.

### Reading the repo as each audience

The same repository has to answer two different first questions. Both must be answerable within a
minute of landing on it.

| Reader | First question | Where they should land |
|---|---|---|
| Backend hiring manager | "Can this person build a system that survives failure?" | `docs/06-runtime-view.md` (the saga), `docs/failure-modes.md`, the chaos-test results |
| Data hiring manager | "Does this person understand streaming correctness?" | `docs/08-cross-cutting-concepts/partition-strategy.md`, the Flink job READMEs, the exactly-once proof |

Practical consequence: the README needs **two entry paths**, not one narrative. Add a short
"Start here" block with two links — one per audience — during month 6 packaging.

### The standing rule

Before adding anything to this plan, answer both halves:

> **Which track does this serve, and does that track need it more than the two-to-three weeks it costs?**

If the answer is "neither strongly" — as it was for a full notification service — it does not go in.
If it serves only one track, it has to displace something on that same track rather than eating the
other track's budget.

---

## 18. Order items and master data — copy or look up

An order has to show what was bought: item name, unit price, tax rate. That data lives in the
catalogue, which is fed by the batch pipeline in [section 16](#16-batch-pipeline--master-data-ingestion).
So the question is how an order gets hold of it — and the answer is not the obvious one.

### The decision

> **An order stores a copy of the item data as it was at the moment of purchase.
> It never looks the product up again afterwards.**

The tempting alternative — store `product_id` and join to the catalogue whenever the order is
displayed — is wrong, and wrong in a way that is invisible until it causes real damage:

| What happens later | Consequence of looking it up |
|---|---|
| The price rises from $20 to $25 | Every historical order silently reprices. Last month's revenue report changes. |
| A product is renamed | Old invoices show a name the customer never saw |
| A product is delisted | Old orders break, or render blank |
| A supplier changes the tax rate | Historical tax calculations become unreproducible |

An order is a **financial record of an agreement**. It must be readable and identical in five years,
even if the product it refers to no longer exists. That makes this denormalization deliberate, not
sloppy — the same reason a paper receipt doesn't contain a pointer to a price list.

### What gets copied, and what deliberately does not

| Field | Copied into `OrderItem`? | Why |
|---|---|---|
| `product_id` | ✅ | The link, for analytics and support |
| `product_name` | ✅ | What the customer saw |
| `sku` | ✅ | Stable identifier for fulfilment |
| `unit_price` + `currency` | ✅ | What they agreed to pay |
| `tax_rate` | ✅ | Tax must be reproducible years later |
| `quantity` | ✅ | Belongs to the order, not the product |
| **`category`, `brand`, `supplier`** | ❌ | **Analytical dimensions — not part of the agreement** |
| `cost_price`, `margin` | ❌ | Internal, changes over time, not the customer's business |
| `description`, `images` | ❌ | Presentation, fetched live when displaying |

That split is the important part, and it resolves what looks like a contradiction with section 16.

> **Transactional truth is snapshotted. Analytical dimensions are joined.**
>
> The order records *what was sold and at what price*. The Flink enrichment join supplies
> *what kind of thing it was* — category, brand, supplier. Both are needed; neither replaces the other.

And this is exactly where section 16's join options stop being theoretical:

| Analytical question | Join type |
|---|---|
| "Revenue by category **this quarter**, using today's taxonomy" | **Broadcast state** — current master data |
| "Revenue by the category each product was in **when it sold**" | **Temporal table join** — SCD Type 2 history |

Two legitimate questions, two different joins, from the same order stream. Being able to explain that
distinction is a strong answer to a question most candidates fumble.

### The full path, end to end

```
  Upstream source system
        │  nightly batch (section 16)
        ▼
  bronze → silver → gold
        │
        ▼
  ╔════════════════════════════════════════════════╗
  ║ KAFKA (compacted)  catalog.product.master.v1   ║
  ╚══════╤═══════════════╤═══════════════╤═════════╝
         │               │               │
         ▼               ▼               ▼
      catalog-service              order-service
   (products + search index)     (local read model,
                                   products only)
                                             │
                            checkout ────────┘
                                 │  read price + name + tax NOW
                                 ▼
                        ┌──────────────────┐
                        │    OrderItem     │  ← frozen copy, never updated again
                        └──────────────────┘
                                 │
                                 ▼
                     ordering.order.v1  (event stream)
                                 │
                                 ▼
                            Flink  ⋈  master data   ← adds category/brand/supplier
```

### Why order-service keeps its own product read model

At checkout, `order-service` needs current price and name. Two ways to get them:

| Approach | Trade-off |
|---|---|
| **gRPC call to catalog-service at checkout** | Simple. But checkout — the money path — now fails whenever catalog is down. |
| **A local read model fed by the compacted topic** ★ | Checkout survives catalog being down. Costs one more consumer and one more table. |

Take the second. The justification is the one that matters commercially: **you do not let the
checkout path fail because a catalogue service is restarting.** This is also the cleanest
demonstration of why a compacted topic exists — a new consumer reads it from the beginning and
arrives at complete current state with no bespoke backfill.

It is **not** a violation of service ownership. `order-service` is not reading catalog's database; it
is consuming a published contract and maintaining its own projection. That distinction is precisely
what section 10's rule protects.

### Where the basket lives — decided

**There is no basket service.** The basket is client-side state; the browser holds it and sends
the price it displayed when the customer checks out.

| Option | Verdict |
|---|---|
| **Client-side, server re-validates** ★ | Chosen. Zero services, and it forces an explicit trust boundary. |
| A `basket-service` on Redis (eShop's approach) | An eighth service. Redis is already used for caching in `catalog-service`, so a basket service is not needed to justify it. |
| A draft `Order` inside order-service | Puts pre-purchase browsing state inside an aggregate whose whole purpose is to be an immutable financial record. |

The consequence is a security property worth being able to state out loud:

> **The price arriving from the client is a claim, not a fact.** It is compared, never trusted.
> A client sending `unit_price: 0.01` must be rejected by validation, not by good manners.

Persistent baskets across devices would need a service. That is a product feature nobody is asking
for here, and section 11 keeps it out.

### Price validation at checkout — the part worth getting right

A local read model can be seconds stale, and the client's basket may be hours stale. So:

1. The client sends `price_shown` — what the customer actually saw
2. At checkout, `order-service` compares it against its current read model
3. If they differ, that is a **business decision, not a technical one** — pick one and write it down:
   - Honour the shown price (customer-friendly; the plan's default)
   - Reject and re-prompt (correct; irritating)
   - Honour within a tolerance, re-prompt beyond it (what most real shops do)
4. Whatever is decided, **the agreed price is what gets frozen into `OrderItem`**

> Write this into `docs/09-architecture-decisions/00X-price-at-checkout.md`. Interviewers like this
> question because it has no purely technical answer, and candidates who recognise that stand out.

### Staged delivery — the schema is right from month 1

Master data doesn't arrive until month 4.5, but orders need item details in month 1. The fix is to
get the **shape** right immediately and swap the **source** later:

| Month | Where item data comes from | `OrderItem` schema |
|---|---|---|
| **1** | Seeded product table inside order-service | Final — all snapshot fields present |
| **2** | catalog-service exists; order-service consumes its events into a local read model | Unchanged |
| **4.5** | The read model switches to `catalog.product.master.v1` from the batch pipeline | Unchanged |

**Nothing downstream is rewritten**, because the snapshot logic never changes — only where the source
data originates. Getting the `OrderItem` shape right in month 1 is what buys that.

### Roadmap additions

**Month 1**
- [ ] `OrderItem` carries the full snapshot: `product_id`, `product_name`, `sku`, `unit_price`,
      `currency`, `tax_rate`, `quantity`
- [ ] A test proving an order is **immutable under product change** — change the seeded product,
      re-read the order, assert nothing moved. This is the test that stops someone "helpfully"
      normalising it later.

**Month 2**
- [ ] `order-service` maintains a local product read model
- [ ] Price validation at checkout + the ADR recording which policy was chosen

**Month 4.5**
- [ ] Switch the read model's source to the compacted master-data topic
- [ ] Confirm the immutability test from month 1 **still passes** — this is the regression that proves
      the staged migration was safe

---

## What to do RIGHT NOW

Before writing the first line of code:

0. **`mise.toml`** — pin all four toolchains. Ten minutes; saves you dozens of hours of environment debugging.
1. **`docs/08-cross-cutting-concepts/service-boundaries.md`** — what data each service owns, and what it is **not** allowed to know
2. **`docs/08-cross-cutting-concepts/event-catalog.md`** — a table of event · schema · producer · consumer · partition key
3. **`docs/09-architecture-decisions/001-why-polyglot.md`** — why each language, with the trade-offs stated honestly
4. The arc42 `docs/` skeleton — 12 empty files with headings. Having the skeleton makes writing far easier than facing a blank page.
5. **`CLAUDE.md` + `.agents/skills/proto-contract/` + `.agents/skills/ddd-dotnet/`** — see [section 15](#15-agent-skills--pinning-ai-guidance-into-the-repo). Written before any code exists, they make the AI produce correctly-shaped code from the very first file.

> With service boundaries and an event catalog settled, `proto/` comes out right the first time — and once `proto/` is right, every service built on it is right too.
