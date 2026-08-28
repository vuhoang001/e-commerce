---
name: ddd-dotnet
description: >-
  Domain modelling rules for the C# services in this repository. These are the same
  rules tests/arch/dotnet enforces — the skill and the tests must never disagree.
  USE FOR: creating or editing anything under Domain/ or Application/, adding an
  aggregate, entity, value object or domain event, writing a command or query handler,
  adding a repository, resolving an architecture-test failure.
  DO NOT USE FOR: proto or contract changes (use proto-contract); Kafka producers and
  consumers (use kafka-conventions); gateway endpoints, which hold no domain logic.
metadata:
  version: "1.0"
---

# DDD in the .NET services

## Layer rules — enforced by tests/arch/dotnet

```
Api  →  Application  →  Domain
             ↓
       Infrastructure  →  Domain
```

| Rule | Why |
|---|---|
| `Domain/` references **nothing** — not `Infrastructure/`, not `building-blocks/`, not EF Core | The domain must be testable with no database and no framework. If a domain test needs a container, the dependency is wrong. |
| `Application/` may reference `Domain/` and interfaces only | Handlers orchestrate; they do not know about SQL or Kafka. |
| `Infrastructure/` implements interfaces declared in `Domain/` | Dependency inversion. The domain declares what it needs; infrastructure supplies it. |
| No service references another service's projects | Cross-service communication is gRPC or Kafka. Always. |

## Aggregates

- One aggregate per transaction. Two aggregates changed in one `SaveChanges` is a design error.
- Aggregates inherit `Entity` and are the only entry point — never load a child entity directly.
- Invariants are enforced **inside** the aggregate, never in a handler. A handler that validates
  business rules has taken logic that belongs in the domain.
- Constructors are private; creation goes through a named factory method that states intent
  (`Order.Place(...)`, not `new Order()`).

## Value objects

- Immutable `record` types, compared by value.
- Anything with a unit or a format is a value object, not a primitive: `Money`, `Address`, `Sku`.
- `decimal price` in a signature is a smell — money without a currency is a bug waiting to happen.

## Domain events

- Immutable `record`, past tense: `OrderPlaced`, not `PlaceOrder`.
- Raised **inside** the aggregate, dispatched after `SaveChanges` — never published mid-transaction.
- Domain events are in-process. Integration events cross the network and are separate types
  defined in `proto/events/`. Do not conflate them; do not publish a domain event to Kafka.

## Handlers

- One handler per command or query, in `Application/`.
- Cross-cutting behaviour lives in pipeline behaviours (`Logging → Validation → Transaction`),
  never repeated inside handlers.
- Handlers return a result type. Control flow through exceptions is for genuinely exceptional cases.

## OrderItem — do not normalise

`OrderItem` deliberately copies `product_name`, `unit_price`, `tax_rate` from the catalogue at
purchase time. This is not a missed normalisation; it is what makes an order a permanent financial
record. See `PLAN.md` section 18. A test guards it — if that test fails, the test is right.
