# ADR-004 — The gateway groups code by feature, order-service by layer

**Status:** Accepted
**Date:** 2026-08-28

## Context

The gateway was first written as `src/Api/{Contracts,Endpoints}/` — folders named after
what the code *is* rather than what it *serves*. Three problems surfaced immediately.

`Contracts/` collided with a word this repository has already spent. CLAUDE.md, PLAN.md
section 4 and the `proto-contract` skill all state that `proto/` is the single source of
truth for every contract, and `building-blocks/contracts-csharp/` holds the generated
stubs. A third `Contracts/` holding hand-written JSON records is the only one of the
three that has nothing to do with protobuf.

The `Api/` level had nothing to distinguish it from. order-service has one because it is
one of four layers; the gateway has no layers, so `api-gateway/src/Api` only says "api"
twice. PLAN.md section 3b never had it.

Grouping by technical role meant one new public feature touched every folder. The gateway
is a backend-for-frontend: a set of independent public features — orders, products,
basket — that share infrastructure but not logic.

## Decision

**The gateway groups by feature.** One folder per public feature, holding its endpoint,
its response records and its mapping together:

```
src/Orders/{OrderEndpoints,OrderResponse,OrderMapping}.cs
```

**Genuinely cross-cutting concerns stay technical**: `Clients/` (gRPC client
registration, and from month 2 the Polly policies) and `Middleware/` (auth, rate
limiting, correlation id) apply to every feature regardless of which one calls them.

**The public records are named `*Response`**, not `*Dto`. Every one of them is a DTO, so
the suffix carries no information; `OrderResponse` says what it is and reads correctly
beside a future `OrderRequest`.

**order-service keeps its layer folders** — `Domain/`, `Application/`, `Infrastructure/`,
`Api/` — because its layers carry enforced dependency rules that `tests/arch/dotnet`
checks in month 1. Feature folders there would dissolve the boundary the tests exist to
protect.

## Consequences

Easier: adding a feature means adding one folder. In month 3 `Products/` arrives with its
fan-out to search, recommendation and inventory, and touches nothing else.

Easier: the word "contract" means one thing again — proto.

Harder: two conventions in one repository, which needs explaining to anyone new. The rule
is the trade-off itself: **group by layer where dependency rules are enforced, group by
feature where they are not.**

Harder: a type used by two features has no obvious home. `MoneyResponse` lives in
`Orders/` today because orders are its only user; it moves when a second feature needs
it, not in anticipation.

## Alternatives considered

**Follow PLAN.md section 2 literally** (`Endpoints/`, `Clients/`, `Middleware/`, with the
records in `Models/`). Rejected: it keeps the split-by-role problem, and `Models/` is
nearly as vague as `Contracts/`. PLAN.md section 2 has been updated to match this ADR.

**Keep the folders, rename `Contracts/` to `Responses/`.** Rejected: it fixes the name
collision, the smallest of the three problems, and leaves the other two.

**Drop the suffix and use plain `Order`.** Rejected: `Order` would then mean a protobuf
message, a domain aggregate from month 1, and a JSON record — three types needing
qualification at nearly every use site.
