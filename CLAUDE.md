# Agent Instructions

## Project language

English only — documentation, code comments, commit messages, ADRs, and identifiers.

## Standard commands — use these, never call dotnet/go/buf directly

| Task | Command |
|---|---|
| First-run setup | `make setup` |
| Build and run everything | `make up` |
| Infrastructure only | `make infra` |
| Tests | `make test` |
| Architecture tests | `make arch` |
| Lint | `make lint` |
| Regenerate proto stubs | `make proto` |

If a task needs a command that isn't here, add a Makefile target rather than
running the tool directly.

## Absolute rules

1. **Never hand-edit anything under `building-blocks/gen/`** — run `make proto`.
2. **Never let one service reference another service's packages or database.**
   Services communicate over gRPC or Kafka, never by reaching in.
3. **Never add a Kafka topic without first adding it to the event catalog**
   (`docs/08-cross-cutting-concepts/event-catalog.md`).
4. **Every architectural decision gets an ADR** in `docs/09-architecture-decisions/`.
5. **Never normalise `OrderItem`.** It snapshots product data on purpose — see
   `PLAN.md` section 18. There is a test guarding this; if it fails, the test is right.

## Before writing code in a specific area

Read the matching skill in `.agents/skills/`. They are short and they encode
decisions already made — following them is faster than rediscovering them.

## The plan

`PLAN.md` is the source of truth for scope and sequencing. If a request conflicts
with it, say so rather than silently doing both.
