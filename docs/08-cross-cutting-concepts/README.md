# 8. Cross-cutting Concepts

Topics that apply across services rather than living inside one.

| Document | Covers | Status |
|---|---|---|
| [service-boundaries.md](./service-boundaries.md) | What each service owns and must not know | 🔴 write first |
| [event-catalog.md](./event-catalog.md) | Every event: schema, producer, consumers, partition key | 🔴 write first |
| partition-strategy.md | Why each topic is keyed the way it is | month 2 |
| schema-evolution.md | What happens when a contract changes | month 2 |
| observability.md | Tracing, metrics, logging across four languages | month 5 |
| data-lineage.md | Source through to consumer, for the batch pipeline | month 4.5 |
