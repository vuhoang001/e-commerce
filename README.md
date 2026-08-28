# e-commerce — Polyglot Microservices Reference

A learning-oriented e-commerce reference system built around **event-driven architecture** and
**real-time stream processing**. Inspired by [dotnet/eShop](https://github.com/dotnet/eShop),
but deliberately diverging where eShop stops short.

> **Status:** 📐 Planning. No code yet — the architecture is being designed first, on purpose.

## Why this exists

Most reference e-commerce apps demonstrate CRUD over microservices. This one is built to
demonstrate the parts that are usually skipped:

- **Kafka** with deliberate partition-key design, replay, and consumer-group semantics
- **CDC via Debezium** reading the Postgres WAL, instead of outbox polling
- **Schema Registry** with breaking-change detection enforced in CI
- **Apache Flink** for stateful stream processing — windowing, keyed state, exactly-once
- **Distributed tracing** that stays unbroken across four languages *and* across Kafka

## Architecture at a glance

| Service | Language | Responsibility |
|---|---|---|
| `api-gateway` | C# / .NET + YARP | BFF, auth, rate limiting, trace origin |
| `order-service` | C# / .NET | DDD aggregates, saga orchestration, outbox |
| `payment-service` | C# / .NET | Saga participant |
| `search-service` | Go | Low-latency search over OpenSearch |
| `inventory-service` | Go | Stock reservation under contention |
| `recommendation-service` | Python | Embeddings, vector similarity |
| `stream-processor` | Java + Flink | Windowing, fraud detection, sessionization |

Contracts are **proto-first**: `proto/rpc/` for synchronous gRPC, `proto/events/` for
asynchronous Kafka messages, with stubs generated for all four languages via `buf`.

## The plan

The full build plan — 6 months, phase by phase, with acceptance criteria — lives in
**[PLAN.md](./PLAN.md)**. It covers repository layout, language-selection rationale,
gRPC-vs-Kafka decision rules, local dev workflow, CI for a polyglot monorepo, known pitfalls,
and explicit out-of-scope boundaries.

Architecture documentation follows the [arc42](https://arc42.org/) template. Toolchain versions
for all four languages are pinned with [mise](https://mise.jdx.dev/), and architectural boundaries
are enforced by automated architecture tests rather than convention alone.

## Roadmap

| Phase | Focus | Status |
|---|---|---|
| 0.5 | Walking skeleton — one request, one trace | ☐ |
| 1 | Core domain (C#) | ☐ |
| 2 | Go services + event backbone | ☐ |
| 3 | Saga orchestration + Python service | ☐ |
| 4 | Flink stream processing | ☐ |
| 5 | Serving layer + end-to-end observability | ☐ |
| 6 | Hardening, chaos testing, deployment, packaging | ☐ |

## Project conventions

- **Language: English.** Documentation, code comments, commit messages, ADRs, and identifiers.
- **Contracts before code.** `proto/` is the single source of truth; `buf breaking` gates every PR.
- **Boundaries are enforced, not documented.** Architecture tests run in CI.
- **Toolchains are pinned.** `mise install` gets all four languages on the right version.

## License

MIT
