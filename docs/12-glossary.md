# 12. Glossary

> **What belongs here:** the ubiquitous language. One agreed meaning per term, used
> identically in code, documentation and conversation.

| Term | Meaning |
|---|---|
| **Order** | A customer's committed purchase. Immutable once placed. |
| **Master data** | Reference data describing things (products, categories), sourced from upstream systems, as opposed to events recording what happened. |
| **Integration event** | A fact published to Kafka for other services. Distinct from a domain event, which never leaves its process. |
| **Snapshot** | A copy of data taken at a business moment and never refreshed — see PLAN.md section 18. |

_TODO — add terms as they appear. If two people use one word differently, it belongs here._
