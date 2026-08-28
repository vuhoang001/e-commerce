---
name: proto-contract
description: >-
  Rules for changing anything under proto/ in this repository — the single source
  of truth for every service contract.
  USE FOR: adding or editing a .proto file, adding an RPC, adding an integration
  event, renaming a field, versioning a contract, regenerating stubs, resolving a
  buf lint or buf breaking failure.
  DO NOT USE FOR: Kafka runtime behaviour such as partitioning, consumer groups or
  DLQs (use kafka-conventions); domain modelling (use ddd-dotnet).
metadata:
  version: "1.0"
---

# Proto contracts

## The workflow — never skip a step

```
edit proto/  →  buf lint  →  buf breaking  →  make proto  →  commit gen/ WITH the change
```

`make proto` runs lint and generate together. Run `make proto-check` before opening a PR.

## Absolute rules

1. **Never hand-edit `building-blocks/gen/`.** It is generated and marked
   `linguist-generated`. Editing it means the next `make proto` silently reverts your work.
2. **Commit generated code**, in the same commit as the `.proto` change. A fresh clone
   must build without installing `buf`.
3. **The version lives in the path**: `order/v1/order_service.proto`. Never `order_v2.proto`.
4. **Breaking changes create a new version** — add `v2/`, run both, deprecate `v1` gradually.
   Never edit `v1` into incompatibility.

## rpc/ and events/ are different in kind

| Directory | Meaning | Naming |
|---|---|---|
| `proto/rpc/` | "I am asking you for this" | Verbs — `GetOrder`, `ReserveStock` |
| `proto/events/` | "This already happened" | Past tense — `OrderPlaced`, `StockReserved` |

Getting this wrong produces events that read like commands, which invites consumers to
treat them as instructions rather than facts.

## Before adding to proto/events/

Add the event to `docs/08-cross-cutting-concepts/event-catalog.md` **first**: name, schema,
producer, consumers, partition key. No catalog entry means no event.

## Field rules

- Never reuse a field number, and never renumber. Use `reserved` when removing one.
- New fields are optional and additive. Anything else is a breaking change.
- Money is `common/v1/money.proto` — amount plus currency, never a bare `double`.
- Timestamps are `google.protobuf.Timestamp`, always UTC.
